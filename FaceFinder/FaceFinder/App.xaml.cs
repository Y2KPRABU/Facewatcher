using System.Windows;

using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.Face;

namespace FaceFinder
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IComputerVisionClient computerVisionClient;
        public IFaceClient faceClient;
        static QueueHandler qh;
        public void SetupComputerVisionClient(string key, string endpoint)
        {
            computerVisionClient = new ComputerVisionClient(
                new Microsoft.Azure.CognitiveServices.Vision.ComputerVision.ApiKeyServiceClientCredentials(key),
                new System.Net.Http.DelegatingHandler[] { });
            computerVisionClient.Endpoint = endpoint;
        }

        public async void SetupFaceClient(string key, string endpoint)
        {
            faceClient = new FaceClient(
                new Microsoft.Azure.CognitiveServices.Vision.Face.ApiKeyServiceClientCredentials(key),
                new System.Net.Http.DelegatingHandler[] { });
            faceClient.Endpoint = endpoint;
             qh = new QueueHandler();
                await  qh.SendMessages();
                await qh.ReceiveMsgs();
               //await qh.DisposeResources();
        }
    }
}
