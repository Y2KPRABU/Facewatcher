using System;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Messaging.ServiceBus;

namespace FaceFinder;

public class QueueHandler
{
    // name of your Service Bus queue
    // the client that owns the connection and can be used to create senders and receivers
    static ServiceBusClient client;

    // the sender used to publish messages to the queue
    static ServiceBusSender sender;
      // create a processor that we can use to process the messages
     static  ServiceBusProcessor processor ;


    // number of messages to be sent to the queue
    const int numOfMessages = 1;
    const string SqueueName = "sunsbusq";
    const string RqueueName = "qresponse";

    public async Task SendMessages()
    {
        // The Service Bus client types are safe to cache and use as a singleton for the lifetime
        // of the application, which is best practice when messages are being published or read
        // regularly.
        //
        // Set the transport type to AmqpWebSockets so that the ServiceBusClient uses the port 443. 
        // If you use the default AmqpTcp, ensure that ports 5671 and 5672 are open.
        var clientOptions = new ServiceBusClientOptions
        {
            TransportType = ServiceBusTransportType.AmqpWebSockets,

        };
        //TODO: Replace the "<NAMESPACE-NAME>" and "<QUEUE-NAME>" placeholders.
        client = new ServiceBusClient(
            "Endpoint=sb://sunsbus.servicebus.windows.net/;SharedAccessKeyName=listenonlypolicy;SharedAccessKey=bhftACybtr9k1ShG/ZidyQT4fLIAvtgtM+ASbB00f6Q=",
            clientOptions);
        sender = client.CreateSender(SqueueName);
        // create a batch 
        ServiceBusMessage sbmessage = new ServiceBusMessage("message sundar");
        //sbmessage.Subject


        
        
            // Use the producer client to send the batch of messages to the Service Bus queue
            await sender.SendMessageAsync(sbmessage);
            Console.WriteLine($"A batch of {numOfMessages} messages has been published to the queue.");
        
       
    }

    public async Task ReceiveMsgs()
    {
        // create the options to use for configuring the processor
        var options = new ServiceBusProcessorOptions
        {
            // By default or when AutoCompleteMessages is set to true, the processor will complete the message after executing the message handler
            // Set AutoCompleteMessages to false to [settle messages](/azure/service-bus-messaging/message-transfers-locks-settlement#peeklock) on your own.
            // In both cases, if the message handler throws an exception without settling the message, the processor will abandon the message.
            AutoCompleteMessages = true, ReceiveMode= ServiceBusReceiveMode.PeekLock,

            // I can also allow for multi-threading
            MaxConcurrentCalls = 1
        };
        processor = client.CreateProcessor(RqueueName, options);
      
        // configure the message and error handler to use
        processor.ProcessMessageAsync += MessageHandler;
        processor.ProcessErrorAsync += ErrorHandler;
       
            // start processing
            await processor.StartProcessingAsync();
       
    }
     async Task MessageHandler(ProcessMessageEventArgs args)
        {
            string body = args.Message.Body.ToString();
            Console.WriteLine(body);

            // we can evaluate application logic and use that to determine how to settle the message.
            await args.CompleteMessageAsync(args.Message);
        }
     Task ErrorHandler(ProcessErrorEventArgs args)
        {
            // the error source tells me at what point in the processing an error occurred
            Console.WriteLine(args.ErrorSource);
            // the fully qualified namespace is available
            Console.WriteLine(args.FullyQualifiedNamespace);
            // as well as the entity path
            Console.WriteLine(args.EntityPath);
            Console.WriteLine(args.Exception.ToString());
            return Task.CompletedTask;
        }
    public async Task DisposeResources()
    {
     
            // Calling DisposeAsync on client types is required to ensure that network
            // resources and other unmanaged objects are properly cleaned up.
            await sender.DisposeAsync();
            await client.DisposeAsync();
      
    }
}
