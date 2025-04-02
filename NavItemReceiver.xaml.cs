using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using Windows.Devices.Enumeration;
using Windows.Devices.WiFiDirect;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LocalDrop
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class NavItemReceiver : Page
    {
        WiFiDirectAdvertisementPublisher _publisher = new WiFiDirectAdvertisementPublisher();
        DeviceWatcher _deviceWatcher = null;
        bool _fWatcherStarted = false;
        FileReceiver receiver = new FileReceiver();
        WiFiDirectConnectionListener connectionListener = new WiFiDirectConnectionListener();
        public NavItemReceiver()
        {
            this.InitializeComponent();
            BeginBroadcast();
            //InitializeSocketListener();
        }


        private void BeginBroadcast()
        {
            if (_deviceWatcher == null)
            {
                _publisher.Advertisement.IsAutonomousGroupOwnerEnabled = true;
                _publisher.Advertisement.ListenStateDiscoverability =
                    WiFiDirectAdvertisementListenStateDiscoverability.Normal;
                Debug.WriteLine("��ʼ�㲥");
                _publisher.Start();
                if (_publisher.Status != WiFiDirectAdvertisementPublisherStatus.Started)
                {
                    Debug.WriteLine("�㲥ʧ��");
                }

                //AssociationEndpoint �������ս�㡣 ������������ԡ�ƽ����Ժ��ֻ���
                //DeviceInterface �豸�ӿڡ�
                WiFiDirectDeviceSelectorType wiFiDirectDeviceSelectorType = WiFiDirectDeviceSelectorType.AssociationEndpoint;
                //string deviceSelector = WiFiDirectDevice.GetDeviceSelector(wiFiDirectDeviceSelectorType);
                string deviceSelector = WiFiDirectDevice.GetDeviceSelector(wiFiDirectDeviceSelectorType);
                _deviceWatcher = DeviceInformation.CreateWatcher(
                    deviceSelector,
                    new string[] { "System.Devices.WiFiDirect.InformationElements" }
                );
                connectionListener.ConnectionRequested += ConnectionRequestedHandler;
            }
            else
            {
                _publisher.Stop();
                StopWatcher();
                Debug.WriteLine("ֹͣ�㲥");
            }

        }
        bool is_listen = false;
        private async void ConnectionRequestedHandler(
      WiFiDirectConnectionListener sender,
      WiFiDirectConnectionRequestedEventArgs args)
        {
            var request = args.GetConnectionRequest();
            try
            {
                var device = await WiFiDirectDevice.FromIdAsync(request.DeviceInformation.Id);
                if (device.ConnectionStatus == WiFiDirectConnectionStatus.Connected)
                {
                    var endpointPairs = device.GetConnectionEndpointPairs();
                    foreach (var pair in endpointPairs)
                    {
                        // �����ļ�������
                        if (receiver.IsListening == false)
                        {
                            Debug.WriteLine($"LocalServiceName:{pair.LocalServiceName} RemoteServiceName:{pair.RemoteServiceName}");
                            Debug.WriteLine($"LocalHostName:{pair.LocalHostName} RemoteHostName:{pair.RemoteHostName}");

                            await receiver.StartAsync(27431);

                        }

                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"��Դ���: {ex.Message}");
            }


        }



        public void Stop()
        {
            _publisher?.Stop();
            Console.WriteLine("��ֹͣ���з���");
        }
        private void StopWatcher()
        {

            _deviceWatcher = null;
            connectionListener.ConnectionRequested -= ConnectionRequestedHandler;
        }


    }
}
