﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Common.ViewModels;
using log4net;
using Microsoft.Owin.BuilderProperties;
using Microsoft.Owin.Hosting;
using SignalRSelfHost.Hubs.Ticker;

namespace SignalRSelfHost
{
    public class MainWindowViewModel : IMainWindowViewModel
    {
        private const string Address = "http://localhost:5263";
        
        private readonly ITickerPublisher m_tickerPublisher;
        private static readonly ILog Log = LogManager.GetLogger(typeof(MainWindowViewModel));
        private IDisposable signalr;

        public MainWindowViewModel(ITickerPublisher m_tickerPublisher)
        {
            this.m_tickerPublisher = m_tickerPublisher;

            AutoTickerStartCommand = new DelegateCommand(m_tickerPublisher.Start);
            AutoTickerStopCommand = new DelegateCommand(m_tickerPublisher.Stop);
            SendOneTickerCommand = new DelegateCommand(async () =>
            {
                await m_tickerPublisher.SendOneManualFakeTicker();
            });
            StartCommand = new DelegateCommand(StartServer);
            StopCommand = new DelegateCommand(StopServer);
        }

        public ICommand AutoTickerStartCommand { get; set; }
        public ICommand AutoTickerStopCommand { get; set; }
        public ICommand SendOneTickerCommand { get; set; }
        public ICommand StartCommand { get; private set; }
        public ICommand StopCommand { get; private set; }

        public void Start()
        {
            StartServer();

           
        }
       



        private void StartServer()
        {

            try
            {
                signalr = WebApp.Start(Address);
            }
            catch (Exception exception)
            {
                Log.Error("An error occurred while starting SignalR", exception);
            }
        }


        private void StopServer()
        {
            if (signalr != null)
            {
                signalr.Dispose();
                signalr = null;
            }

        }

    }
}
