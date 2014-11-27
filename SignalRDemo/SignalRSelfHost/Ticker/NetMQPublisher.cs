using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common;
using NetMQ;
using NetMQ.Actors;
using NetMQ.InProcActors;
using NetMQ.Sockets;

namespace SignalRSelfHost.Ticker
{
    public class NetMQPublisher : ITickerPublisher
    {
        private const string PublishTicker = "P";        

        public class ShimHandler : IShimHandler<object>
        {
            private readonly NetMQContext context;
            private PublisherSocket publisherSocket;
            private ResponseSocket snapshotSocket;
            private ITickerRepository tickerRepository;
            private Poller poller;            

            public ShimHandler(NetMQContext context, ITickerRepository tickerRepository)
            {
                this.context = context;
                this.tickerRepository = tickerRepository;                
            }

            public void Initialise(object state)
            {

            }

            public void RunPipeline(PairSocket shim)
            {
                publisherSocket = context.CreatePublisherSocket();
                publisherSocket.Bind("tcp://*:" + StreamingProtocol.Port);

                snapshotSocket = context.CreateResponseSocket();
                snapshotSocket.Bind("tcp://*:" + SnapshotProtocol.Port);
                snapshotSocket.ReceiveReady += OnSnapshotReady;
                
                shim.ReceiveReady += OnShimReady;

                shim.SignalOK();

                poller = new Poller();
                poller.AddSocket(shim);
                poller.Start();
            }

            private void OnSnapshotReady(object sender, NetMQSocketEventArgs e)
            {                
                string command = snapshotSocket.ReceiveString();

                // Currently we only have one type of events
                if (command == SnapshotProtocol.GetTradessCommand)
                {
                    var tickers = tickerRepository.GetAllTickers();

                    // we will send all the tickers in one message
                    foreach (var ticker in tickers)
                    {
                        snapshotSocket.SendMore(ticker.Name).SendMore(ticker.Price.ToString());
                    }

                    snapshotSocket.Send(SnapshotProtocol.EndOfTickers);
                }
            }

            private void OnShimReady(object sender, NetMQSocketEventArgs e)
            {
                string command = e.Socket.ReceiveString();

                switch (command)
                {
                    case ActorKnownMessages.END_PIPE:
                        poller.Stop();
                        break;
                    case PublishTicker:
                        string topic = e.Socket.ReceiveString();
                        string name = e.Socket.ReceiveString();
                        string price = e.Socket.ReceiveString();
                        publisherSocket.
                            SendMore(topic).
                            SendMore(name).
                            Send(price);
                        break;
                }
            }
        }

        private Actor<object> actor;
        private readonly NetMQContext context;
        private readonly ITickerRepository tickerRepository;
                    
        public NetMQPublisher(NetMQContext context, ITickerRepository tickerRepository)
        {
            this.context = context;
            this.tickerRepository = tickerRepository;        
        }

        public void Start()
        {
            actor = new Actor<object>(context, new ShimHandler(context, tickerRepository), null);
        }

        public void Stop()
        {
            actor.Dispose();
        }        

        public void PublishTrade(TickerDto ticker)
        {
            actor.
                SendMore(PublishTicker).
                SendMore(StreamingProtocol.TradesTopic).
                SendMore(ticker.Name).
                Send(ticker.Price.ToString());
        }
    }
}
