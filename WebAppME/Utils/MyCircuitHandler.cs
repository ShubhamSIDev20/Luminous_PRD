using Microsoft.AspNetCore.Components.Server.Circuits;

namespace BatteryTestingSystem.Utils
{
    public class MyCircuitHandler : CircuitHandler
    {
        public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            Console.WriteLine("UI Circuit connected!");
            return Task.CompletedTask;
        }

        public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            Console.WriteLine("UI Circuit disconnected.");
            return Task.CompletedTask;
        }
    }
}
