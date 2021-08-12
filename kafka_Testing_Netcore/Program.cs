using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;


using Kafka.Public;
using Confluent.Kafka;
using Kafka.Public.Loggers; // epic

namespace kafka_Testing_Netcore
{
    class Program
    {
        static void Main(string[] args)
        {
           CreateHostBuilder(args).Build().Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, collection) =>
                {
                    collection.AddHostedService<KafkaConsumerHostedService>();
                    collection.AddHostedService<KafkaProducerHostedService>();
                });
    }

    public class KafkaConsumerHostedService : IHostedService
    {
        private readonly ILogger<KafkaConsumerHostedService> _logger;
        private readonly ClusterClient _cluster;

        public KafkaConsumerHostedService(ILogger<KafkaConsumerHostedService> logger)
        {
            _logger = logger;
            _cluster = new ClusterClient(new Configuration
            {
                Seeds = "localhost:9092"
            }, new ConsoleLogger());
        }
        
        public Task StartAsync(CancellationToken cancellationToken)
        {
            // here we will begin consuming messages
            _cluster.ConsumeFromLatest("demo"); // this utilizes kafka-sharp; more lightweight, no deserializer
            _cluster.MessageReceived += record =>
            {
                _logger.LogInformation($"Recieved: {Encoding.UTF8.GetString(record.Value as byte[])}");
                // record.value says its an object but it is really a byte array - cast it to a byte array!
                if (Encoding.UTF8.GetString(record.Value as byte[]).Contains("50"))
                {
                    _logger.LogInformation("Specific Message Recieved!");
                    // Write to ksqlDB table regarding specific details of order, etc.
                }
            };
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cluster?.Dispose();
            return Task.CompletedTask;
        }
    }
    
    public class KafkaProducerHostedService : IHostedService
    {
        private readonly ILogger<KafkaProducerHostedService> _logger;
        private readonly IProducer<Null, string> _producer;

        public KafkaProducerHostedService(ILogger<KafkaProducerHostedService> logger)
        {
            _logger = logger;
            var config = new ProducerConfig() //specify bootstrap servers, localhost:9092 - configured for docker conts
            {
                BootstrapServers = "localhost:9092"
            };
            _producer = new ProducerBuilder<Null, string>(config).Build();
        }
        
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            for (var i = 0; i < 100; ++i)
            {
                var value = $"Hello World {i}";
                _logger.LogInformation(value);
                // use producer to send that message - holds topic and message
                await _producer.ProduceAsync("demo", new Message<Null, string>()
                {
                    Value = value
                }, cancellationToken);
            }

            _producer.Flush(TimeSpan.FromSeconds(10));
        }
        
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _producer?.Dispose();
            return Task.CompletedTask;
        }
    }
    
}