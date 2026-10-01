namespace BatteryTestingSystem.Services.Implementations
{

    public sealed class ScopedService<T> : IDisposable where T : notnull
    {
        public IServiceScope Scope { get; }
        public T Service { get; }

        public ScopedService(IServiceScope scope, T service)
        {
            Scope = scope;
            Service = service;
        }

        public void Dispose()
        {
            Scope.Dispose(); // disposing scope disposes DbContext + dependencies
        }
    }


    public static class ServiceLocator
    {
        private static IServiceProvider? _provider;

        public static void SetProvider(IServiceProvider provider)
        {
            _provider = provider;
        }

        public static IServiceScope CreateScope()
        {
            if (_provider == null)
                throw new InvalidOperationException("ServiceProvider not initialized");

            return _provider.CreateScope();
        }

        public static ScopedService<T> GetScoped<T>() where T : notnull
        {
            var scope = CreateScope();

            var service = scope.ServiceProvider.GetRequiredService<T>();

            return new ScopedService<T>(scope, service);
        }

     

    }

}
