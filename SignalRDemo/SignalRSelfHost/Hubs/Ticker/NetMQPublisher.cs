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
using NetMQ.zmq;
using Poller = NetMQ.Poller;

namespace SignalRSelfHost.Hubs.Ticker
{
    public class NetMQPublisher : ITickerPublisher
    {
        private const string PublishTicker = "P";
        private const int Port = 10443;

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
                publisherSocket.Bind("tcp://*:" + Port);

                snapshotSocket = context.CreateResponseSocket();
                snapshotSocket.Bind("tcp://*:" + Port + 1);
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
                        string name = e.Socket.ReceiveString();
                        string price = e.Socket.ReceiveString();
                        publisherSocket.SendMore(name).Send(price);
                        break;
                }
            }
        }

        private Actor<object> actor;
        private readonly NetMQContext context;
        private readonly ITickerRepository tickerRepository;
        private Random rand;
            
        public NetMQPublisher(NetMQContext context, ITickerRepository tickerRepository)
        {
            this.context = context;
            this.tickerRepository = tickerRepository;
            this.rand = new Random();
        }

        public void Start()
        {
            actor = new Actor<object>(context, new ShimHandler(context, tickerRepository), null);
        }

        public void Stop()
        {
            actor.Dispose();
        }

        public async Task SendOneManualFakeTicker()
        {
            var currentTicker = tickerRepository.GetNextTicker();

            var flipPoint = rand.Next(0, 100);

            if (flipPoint > 50)
            {
                currentTicker.Price += currentTicker.Price / 30;
            }
            else
            {
                currentTicker.Price -= currentTicker.Price / 30;
            }

            tickerRepository.StoreTicker(currentTicker);

            Publish(currentTicker.Name, currentTicker.Price);
        }

        public void Publish(string name, decimal price)
        {
            actor.SendMore(PublishTicker).SendMore(name).SendMore(price.ToString());
        }
    }
}
