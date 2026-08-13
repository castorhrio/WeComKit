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
    /// 注册企业微信 API 服务，并允许对 <see cref="HttpClient"/> 进行应用层配置
    /// （Timeout / DefaultRequestHeaders）。
    /// </summary>
    /// <remarks>
    /// 此重载仅支持 <see cref="HttpClient"/> 层面的配置。
    /// <b>注意：</b><see cref="WeComHttpClient"/> 通过 <see cref="WeComOptions.ApiUrl"/> 构造绝对 URI，
    /// 因此在此处设置 <see cref="HttpClient.BaseAddress"/> 不会生效；如需更换 API 地址请配置
    /// <see cref="WeComOptions.ApiUrl"/>。
    /// 如需自定义 <see cref="HttpMessageHandler"/> / 代理 / 主处理器，
    /// 请改用返回 <c>IHttpClientBuilder</c> 的标准方式，例如：
    /// <code>
    /// services.AddHttpClient("WeComKit")
    ///         .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { Proxy = new WebProxy("...") });
    /// </code>
    /// </remarks>
    /// <param name="services">服务集合</param>
    /// <param name="configure">WeComOptions 配置回调</param>
    /// <param name="configureHttpClient"><see cref="HttpClient"/> 配置回调</param>
    public static IServiceCollection AddWeComKit(
        this IServiceCollection services,
        Action<WeComOptions> configure,
        Action<HttpClient> configureHttpClient)
    {
        var options = new WeComOptions();
        configure(options);
        return AddWeComKit(services, options, configureHttpClient);
    }

    /// <summary>
    /// 注册企业微信 API 服务（从配置读取）
    /// </summary>
    public static IServiceCollection AddWeComKit(this IServiceCollection services, WeComOptions options)
    {
        return AddWeComKit(services, options, configureHttpClient: null);
    }

    /// <summary>
    /// 注册企业微信 API 服务（从配置读取，可配置 HttpClient）
    /// </summary>
    public static IServiceCollection AddWeComKit(
        this IServiceCollection services,
        WeComOptions options,
        Action<HttpClient>? configureHttpClient)
    {
        services.AddSingleton(options);
        var clientBuilder = services.AddHttpClient("WeComKit");
        if (configureHttpClient is not null)
            clientBuilder.ConfigureHttpClient(configureHttpClient);

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
