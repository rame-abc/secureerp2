using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SecureERP2.Modules.Assets.Entities;
using SecureERP2.Modules.Assets.Services;

namespace SecureERP2.Modules.Assets.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AssetController : ControllerBase
    {
        private readonly AssetService _assetService;

        public AssetController(AssetService assetService)
        {
            _assetService = assetService;
        }

        [HttpPost]
        public async Task<ActionResult<FixedAsset>> CreateAsset([FromBody] FixedAsset asset)
        {
            try
            {
                var createdAsset = await _assetService.CreateAssetAsync(asset);
                return CreatedAtAction(nameof(GetAsset), new { id = createdAsset.Id }, createdAsset);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<ActionResult<List<FixedAsset>>> GetAllAssets()
        {
            var assets = await _assetService.GetAllAssetsAsync();
            return Ok(assets);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<FixedAsset>> GetAsset(long id)
        {
            var asset = await _assetService.GetAssetAsync(id);
            if (asset == null)
                return NotFound();

            return Ok(asset);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<FixedAsset>> UpdateAsset(long id, [FromBody] FixedAsset asset)
        {
            try
            {
                if (id != asset.Id)
                    return BadRequest("ID mismatch");

                var updatedAsset = await _assetService.UpdateAssetAsync(asset);
                return Ok(updatedAsset);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteAsset(long id)
        {
            var result = await _assetService.DeleteAssetAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpPost("depreciation/run")]
        public async Task<ActionResult<List<DepreciationSchedule>>> RunDepreciation([FromBody] DepreciationRunRequest request)
        {
            try
            {
                List<DepreciationSchedule> schedules;

                if (request.PeriodType == "Monthly")
                {
                    schedules = await _assetService.RunDepreciationAsync(request.PeriodDate, request.PostToLedger);
                }
                else if (request.PeriodType == "Yearly")
                {
                    schedules = await _assetService.RunYearlyDepreciationAsync(request.PeriodDate.Year, request.PostToLedger);
                }
                else
                {
                    return BadRequest("Invalid period type. Use 'Monthly' or 'Yearly'.");
                }

                return Ok(schedules);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("summary")]
        public async Task<ActionResult<Dictionary<string, object>>> GetAssetSummary()
        {
            var summary = await _assetService.GetAssetSummaryAsync();
            return Ok(summary);
        }

        [HttpGet("category/{category}")]
        public async Task<ActionResult<List<FixedAsset>>> GetAssetsByCategory(string category)
        {
            var assets = await _assetService.GetAssetsByCategoryAsync(category);
            return Ok(assets);
        }

        [HttpGet("department/{department}")]
        public async Task<ActionResult<List<FixedAsset>>> GetAssetsByDepartment(string department)
        {
            var assets = await _assetService.GetAssetsByDepartmentAsync(department);
            return Ok(assets);
        }

        [HttpGet("{assetId}/depreciation-schedule")]
        public async Task<ActionResult<List<DepreciationSchedule>>> GetDepreciationSchedule(long assetId)
        {
            var schedule = await _assetService.GetDepreciationScheduleAsync(assetId);
            return Ok(schedule);
        }
    }

    public class DepreciationRunRequest
    {
        public DateTime PeriodDate { get; set; }
        public string PeriodType { get; set; } = "Monthly"; // Monthly or Yearly
        public bool PostToLedger { get; set; } = false;
    }
}
