using Microsoft.Extensions.DependencyInjection;
using WeComKit.Api;
using WeComKit.Api.Http;

namespace WeComKit.Api.Extensions;

/// <summary>
/// WeComKit 依赖注入扩展
/// </summary>
public static class WeComKitServiceExtensions
{
    /// <summary>
    /// 注册企业微信 API 服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置回调</param>
    public static IServiceCollection AddWeComKit(this IServiceCollection services, Action<WeComOptions> configure)
    {
        var options = new WeComOptions();
        configure(options);
        return AddWeComKit(services, options);
    }

    /// <summary>
    /// 注册企业微信 API 服务（从配置读取）
    /// </summary>
    public static IServiceCollection AddWeComKit(this IServiceCollection services, WeComOptions options)
    {
        services.AddSingleton(options);
        services.AddHttpClient("WeComKit");
        services.AddSingleton(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var opts = sp.GetRequiredService<WeComOptions>();
            return new WeComHttpClient(factory.CreateClient("WeComKit"), opts);
        });

        services.AddTransient<WeComContactsApi>();
        services.AddTransient<WeComMessageApi>();
        services.AddTransient<WeComMediaApi>();
        services.AddTransient<WeComOAuthApi>();

        return services;
    }

    /// <summary>
    /// 注册企业微信群机器人服务。
    /// </summary>
    public static IServiceCollection AddWeComBotKit(this IServiceCollection services, Action<WeComBotOptions> configure)
    {
        var options = new WeComBotOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddHttpClient<WeComBotApi>();
        return services;
    }

    /// <summary>
    /// 注册企业微信第三方应用 suite 服务。
    /// </summary>
    public static IServiceCollection AddWeComSuiteKit(this IServiceCollection services, Action<WeComSuiteOptions> configure)
    {
        var options = new WeComSuiteOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddHttpClient<WeComSuiteApi>();
        return services;
    }
}
