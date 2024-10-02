using System.Net;
using API.Converters;
using API.Exceptions;
using API.Infrastructure.Requests;
using API.Settings;
using Core;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Models.API.General;

namespace API.Infrastructure;

public abstract class PresentationController : Controller
{
    protected readonly IMediator Mediator;

    /// <summary>
    ///     API Settings available to derived controller classes
    /// </summary>
    protected readonly ApiSettings Settings;

    /// <inheritdoc />
    protected PresentationController(ApiSettings settings, IMediator mediator)
    {
        this.Settings = settings;
        Mediator = mediator;
    }

    /// <summary>
    ///  Used by derived controllers to construct correct fully qualified URLs in returned objects.
    /// </summary>
    /// <returns></returns>
    protected UrlRoots GetUrlRoots()
    {
        return new UrlRoots
        {
            BaseUrl = Request.GetBaseUrl(),
            ResourceRoot = Settings.ResourceRoot.ToString()
        };
    }

    /// <summary>
    /// Handle an upsert request - this takes a IRequest which returns a ModifyEntityResult{T}.
    /// The request is sent and result is transformed to an http result.
    /// </summary>
    /// <param name="request">IRequest to modify data</param>
    /// <param name="instance">The value for <see cref="JSType.Error.Instance" />.</param>
    /// <param name="errorTitle">
    /// The value for <see cref="JSType.Error.Title" />. In some instances this will be prepended to the actual error name.
    /// e.g. errorTitle + ": Conflict"
    /// </param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <typeparam name="T">Type of entity being upserted</typeparam>
    /// <typeparam name="TEnum">An enum designating the error type</typeparam>
    /// <returns>
    /// ActionResult generated from ModifyEntityResult. This will be the model + 200/201 on success. Or an
    /// error and appropriate status code if failed.
    /// </returns>
    protected async Task<IActionResult> HandleUpsert<T, TEnum>(
    IRequest<ModifyEntityResult<T, TEnum>> request,
    string? instance = null,
    string? errorTitle = "Operation failed",
    CancellationToken cancellationToken = default)
    where T : class
    where TEnum : Enum
    {
        return await HandleRequest(async () =>
        {
            var result = await Mediator.Send(request, cancellationToken);

            return this.ModifyResultToHttpResult(result, instance, errorTitle);
        }, errorTitle);
    }

    /// <summary>
    ///     Handles a deletion
    /// </summary>
    /// <param name="request">The request/response to be sent through Mediatr</param>
    /// <param name="errorTitle">The title of the error</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <see cref="DeleteResult" /> is not understood</exception>
    /// <returns>
    ///     ActionResult generated from DeleteResult. This will be 204 on success. Or an
    ///     error and appropriate status code if failed.
    /// </returns>
    /// <remarks>This will be replaced with overload that takes DeleteEntityResult in future</remarks>
    protected async Task<IActionResult> HandleDelete(
        IRequest<ResultMessage<DeleteResult, DeleteCollectionType>> request,
        string? errorTitle = "Delete failed",
        CancellationToken cancellationToken = default)
    {
        return await HandleRequest(async () =>
        {
            var result = await Mediator.Send(request, cancellationToken);

            return ConvertDeleteToHttp(result.Value, result.Message, result.Type);
            
        }, errorTitle);
    }

    /// <summary>
    ///     Handles a deletion, turning DeleteResult to a http response
    /// </summary>
    /// <param name="request">The request/response to be sent through Mediatr</param>
    /// <param name="errorTitle">The title of the error</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <see cref="DeleteResult" /> is not understood</exception>
    /// <returns>
    ///     ActionResult generated from DeleteResult. This will be 204 on success. Or an
    ///     error and appropriate status code if failed.
    /// </returns>
    protected async Task<IActionResult> HandleDelete(
        IRequest<DeleteEntityResult> request,
        string? errorTitle = "Delete failed",
        CancellationToken cancellationToken = default)
    {
        return await HandleRequest(async () =>
        {
            var result = await Mediator.Send(request, cancellationToken);

            return ConvertDeleteToHttp(result.Value, result.Message, result.Type);
        }, errorTitle);
    }

    private IActionResult ConvertDeleteToHttp<TType>(DeleteResult result, string? message, TType type)
    {
        return result switch
        {
            DeleteResult.NotFound => this.PresentationNotFound(),
            DeleteResult.Conflict => this.PresentationProblem(detail: message, type: GetErrorType(type), statusCode: (int)HttpStatusCode.Conflict, title: "Conflict"),
            DeleteResult.Error => this.PresentationProblem(detail: message, type: GetErrorType(type), statusCode: (int)HttpStatusCode.InternalServerError, title: "Internal Server Error"),
            DeleteResult.BadRequest => this.PresentationProblem(detail: message, type: GetErrorType(type), statusCode: (int)HttpStatusCode.BadRequest, title: "Bad Request"),
            DeleteResult.Deleted => NoContent(),
            _ => throw new ArgumentOutOfRangeException(nameof(DeleteResult), $"No deletion value of {result}")
        };
    }

    public string GetErrorType<TType>(TType type) => $"{GetUrlRoots().BaseUrl}/errors/{type?.GetType().Name}/{type}";

    /// <summary>
    ///     Handle a GET request - this takes a IRequest which returns a FetchEntityResult{T}.
    ///     The request is sent and result is transformed to an http result.
    /// </summary>
    /// <param name="request">IRequest to fetch data</param>
    /// <param name="instance">The value for <see cref="JSType.Error.Instance" />.</param>
    /// <param name="errorTitle">
    ///     The value for <see cref="JSType.Error.Title" />. In some instances this will be prepended to the actual error name.
    ///     e.g. errorTitle + ": Conflict"
    /// </param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <typeparam name="T">Type of entity being fetched</typeparam>
    /// <returns>
    ///     ActionResult generated from FetchEntityResult. This will be the model + 200 on success. Or an
    ///     error and appropriate status code if failed.
    /// </returns>
    protected async Task<IActionResult> HandleFetch<T>(
        IRequest<FetchEntityResult<T>> request,
        string? instance = null,
        string? errorTitle = "Fetch failed",
        CancellationToken cancellationToken = default)
        where T : class
    {
        return await HandleRequest(async () =>
        {
            var result = await Mediator.Send(request, cancellationToken);

            return this.FetchResultToHttpResult(result);
        }, errorTitle);
    }

    /// <summary>
    ///     Make a request and handle exceptions
    /// </summary>
    protected async Task<IActionResult> HandleRequest(Func<Task<IActionResult>> handler,
        string? errorTitle = "Request failed")
    {
        try
        {
            return await handler();
        }
        catch (APIException apiEx)
        {
            return Problem(apiEx.Message, null, apiEx.StatusCode ?? 500, apiEx.Label);
        }
        catch (Exception ex)
        {
            return Problem(ex.Message, null, 500, errorTitle);
        }
    }
}