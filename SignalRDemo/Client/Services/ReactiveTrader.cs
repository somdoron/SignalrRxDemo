using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Client.Factory;
using Client.Hub;
using Client.Hub.Transport;
using Client.Repositories;
using log4net;
using NetMQ;

namespace Client.Services
{
    public class ReactiveTrader : IReactiveTrader, IDisposable
    {
        
        private static readonly ILog log = LogManager.GetLogger(typeof(ReactiveTrader));

        private NetMQContext context;
        private NetMQClient client;


        public void Initialize(string username, string server, string authToken = null)
        {        
            //if (authToken != null)
            //{
            //    var controlServiceClient = new ControlServiceClient(new AuthTokenProvider(authToken), _connectionProvider, _loggerFactory);
            //    _controlRepository = new ControlRepository(controlServiceClient);
            //}

            var concurrencyService = new ConcurrencyService();
            
            context = NetMQContext.Create();
            client = new NetMQClient(context, server);

            var tickerFactory = new TickerFactory();
            TickerRepository = new TickerRepository(client, tickerFactory);
        }

        public ITickerRepository TickerRepository { get; private set; }


        public IObservable<ConnectionInfo> ConnectionStatusStream
        {
            get
            {
                // TODO: status stream doesn't exist yet

                return Observable.Empty<ConnectionInfo>();

                //return _connectionProvider.GetActiveConnection()
                //    .Do(_ => log.Info("New connection created by connection provider"))
                //    .Select(c => c.StatusStream)
                //    .Switch()
                //    .Publish()
                //    .RefCount();
            }
        }

        public void Dispose()
        {
            client.Dispose();
            context.Dispose();
        }
    }

}
