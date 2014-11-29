using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Client.Factory;
using Common;
using NetMQ;
using NetMQ.Actors;
using NetMQ.InProcActors;
using NetMQ.Sockets;
using NetMQ.zmq;
using Newtonsoft.Json;
using Poller = NetMQ.Poller;

namespace Client.Hub
{
    public class NetMQClient :  IDisposable
    {
        private Actor<object> actor;
        private Subject<TickerDto> subject;

        class ShimHandler : IShimHandler<object>
        {
            private NetMQContext context;
            private SubscriberSocket subscriberSocket;
            private Subject<TickerDto> subject;
            private string address;
            private Poller poller;

            public ShimHandler(NetMQContext context, Subject<TickerDto> subject, string address)
            {
                this.context = context;
                this.address = address;
                this.subject = subject;
            }

            public void Initialise(object state)
            {
                
            }

            public void RunPipeline(PairSocket shim)
            {
                // we should signal before running the poller but this will block the application
                shim.SignalOK();

                // getting the snapshot
                using (RequestSocket requestSocket = context.CreateRequestSocket())
                {
                    // TODO: we might want to use time out or trying fetching the snapshot from multiple locations
                    requestSocket.Connect(string.Format("tcp://{0}:{1}", address, SnapshotProtocol.Port));

                    requestSocket.Send(SnapshotProtocol.GetTradessCommand);

                    string json = requestSocket.ReceiveString();

                    while (json != SnapshotProtocol.EndOfTickers)
                    {
                        PublishTicker(json);

                        json = requestSocket.ReceiveString();
                    }
                }

                subscriberSocket = context.CreateSubscriberSocket();
                subscriberSocket.Subscribe(StreamingProtocol.TradesTopic);
                subscriberSocket.Connect(string.Format("tcp://{0}:{1}", address, StreamingProtocol.Port));
                subscriberSocket.ReceiveReady += OnSubscriberReady;

                shim.ReceiveReady += OnShimReady;

                this.poller = new Poller();
                poller.AddSocket(shim);
                poller.AddSocket(subscriberSocket);                

                poller.Start();

                subscriberSocket.Dispose();
            }

            private void OnShimReady(object sender, NetMQSocketEventArgs e)
            {
                string command = e.Socket.ReceiveString();

                if (command == ActorKnownMessages.END_PIPE)
                {
                    poller.Stop(false);
                }
            }

            private void OnSubscriberReady(object sender, NetMQSocketEventArgs e)
            {
                string topic = subscriberSocket.ReceiveString();
                string json = subscriberSocket.ReceiveString();

                PublishTicker(json);
            }

            private void PublishTicker(string json)
            {
                TickerDto tickerDto = JsonConvert.DeserializeObject<TickerDto>(json);
                subject.OnNext(tickerDto);
            }
        }

        public NetMQClient(NetMQContext context, string address)
        {
            subject = new Subject<TickerDto>();

            this.actor = new Actor<object>(context, new ShimHandler(context, subject, address), null);
        }

        public IObservable<TickerDto> GetTickerStream()
        {
            return subject.AsObservable();
        }

        public void Dispose()
        {
            this.actor.Dispose();
        }
    }
}
