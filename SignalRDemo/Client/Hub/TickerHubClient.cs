using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Common;
using NetMQ;

namespace Client.Hub
{
    public class TickerHubClient : ITickerHubClient
    {
        private readonly NetMQContext context;
        private readonly string address;

        public TickerHubClient(NetMQContext context, string address)
        {
            this.context = context;
            this.address = address;
        }

        public IObservable<TickerDto> GetTickerStream()
        {
            return Observable.Create<TickerDto>(observer =>
            {
                NetMQClient client = new NetMQClient(context, address);

                //This netMQClient.GetTickerStream would need to also use the OnError when it could not communicate with the server
                var disposable = client.GetTickerStream().Subscribe(observer);
                return new CompositeDisposable { client, disposable };
            })
            .Publish()
            .RefCount();
        }
    }
}
