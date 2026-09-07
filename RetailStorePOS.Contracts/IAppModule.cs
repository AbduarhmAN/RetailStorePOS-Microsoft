using Microsoft.Extensions.DependencyInjection;

namespace RetailStorePOS.Contracts;

public interface IAppModule
{
    void RegisterServices(IServiceCollection services);
}
