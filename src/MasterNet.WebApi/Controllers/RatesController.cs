using Core.MediatOR.Contracts;
using MasterNet.Application.Core;
using MasterNet.Application.Ratings.GetRatings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using static MasterNet.Application.Ratings.GetRatings.GetRatingsQuery;

namespace MasterNet.WebApi.Controllers;

[ApiController]
[Route("api/ratings")]
public class RatesController : ControllerBase
{
    private readonly IMediatOR _sender;

    public RatesController(IMediatOR sender)
    {
        _sender = sender;
    }

    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public async Task<ActionResult<PagedList<RatingResponse>>> PaginationRatings
    (
        [FromQuery] GetRatingsRequest request,
        CancellationToken cancellationToken
    )
    {
        var query = new GetRatingsQueryRequest
        {
            RatingsRequest = request
        };
        var results = await _sender.Send(query, cancellationToken);
        return results.IsSuccess ? Ok(results.Value) : NotFound();
    }

}
