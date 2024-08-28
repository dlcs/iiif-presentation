using System.Net;
using System.Runtime.InteropServices.JavaScript;
using API.Infrastructure.Requests;
using Core;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace API.Infrastructure;

public static class ControllerBaseX
{
    /// <summary>
    ///     Create an IActionResult from specified FetchEntityResult{T}.
    ///     This will be the model + 200 on success. Or an
    ///     error and appropriate status code if failed.
    /// </summary>
    /// <param name="controller">Current controllerBase object</param>
    /// <param name="entityResult">Result to transform</param>
    /// <typeparam name="T">Type of entity being upserted</typeparam>
    /// <returns>
    ///     ActionResult generated from FetchEntityResult
    /// </returns>
    public static IActionResult FetchResultToHttpResult<T>(this ControllerBase controller,
        FetchEntityResult<T> entityResult)
        where T : class
    {
        if (entityResult.Error)
            return new ObjectResult(entityResult.ErrorMessage)
            {
                StatusCode = 500
            };

        if (entityResult.EntityNotFound || entityResult.Entity == null) return new NotFoundResult();

        return controller.Ok(entityResult.Entity);
    }
    
        /// <summary>
    /// Create an IActionResult from specified ModifyEntityResult{T}.
    /// This will be the model + 200/201 on success. Or an
    /// error and appropriate status code if failed.
    /// </summary>
    /// <param name="controller">Current controllerBase object</param>
    /// <param name="entityResult">Result to transform</param>
    /// <param name="instance">The value for <see cref="JSType.Error.Instance" />.</param>
    /// <param name="errorTitle">
    ///     The value for <see cref="JSType.Error.Title" />. In some instances this will be prepended to the actual error name.
    ///     e.g. errorTitle + ": Conflict"
    /// </param>
    /// <typeparam name="T">Type of entity being upserted</typeparam>
    /// <returns>
    /// ActionResult generated from ModifyEntityResult
    /// </returns>
    public static IActionResult ModifyResultToHttpResult<T>(this ControllerBase controller,
        ModifyEntityResult<T> entityResult, 
        string? instance,
        string? errorTitle)
        where T : class =>
        entityResult.WriteResult switch
        {
            WriteResult.Updated => controller.Ok(entityResult.Entity),
            WriteResult.Created => controller.Created(controller.Request.GetDisplayUrl(), entityResult.Entity),
            WriteResult.NotFound => controller.NotFound(entityResult.Error),
            WriteResult.Error => controller.Problem(entityResult.Error, instance, (int)HttpStatusCode.InternalServerError, errorTitle),
            WriteResult.BadRequest => controller.Problem(entityResult.Error, instance, (int)HttpStatusCode.BadRequest, errorTitle),
            WriteResult.Conflict => controller.Problem(entityResult.Error, instance, (int)HttpStatusCode.Conflict, 
                $"{errorTitle}: Conflict"),
            WriteResult.FailedValidation => controller.Problem(entityResult.Error, instance, (int)HttpStatusCode.BadRequest,
                $"{errorTitle}: Validation failed"),
            WriteResult.StorageLimitExceeded => controller.Problem(entityResult.Error, instance, (int)HttpStatusCode.InsufficientStorage,
                $"{errorTitle}: Storage limit exceeded"),
            _ => controller.Problem(entityResult.Error, instance, (int)HttpStatusCode.InternalServerError, errorTitle),
        };
        
        /// <summary>
        /// Creates an <see cref="ObjectResult"/> that produces a <see cref="Error"/> response with 404 status code.
        /// </summary>
        /// <returns>The created <see cref="ObjectResult"/> for the response.</returns>
        public static ObjectResult ValidationFailed(this ControllerBase controller, ValidationResult validationResult)
        {
            var message = string.Join(". ", validationResult.Errors.Select(s => s.ErrorMessage).Distinct());
            return controller.Problem(message, null, (int)HttpStatusCode.BadRequest, "Bad request");
        }
}