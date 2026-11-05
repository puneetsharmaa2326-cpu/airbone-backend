
using AirborneEnquiryApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace AirborneEnquiryApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EnquiryController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public EnquiryController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // ==========================================
        // DATABASE CONNECTION
        // ==========================================

        private string GetConnectionString()
        {
            return _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "The database connection string is not configured.");
        }


        // ==========================================
        // 1. CREATE ENQUIRY
        // POST: api/Enquiry
        // ==========================================

        [HttpPost]
        public async Task<IActionResult> CreateEnquiry(
            [FromBody] EnquiryModel model)
        {
            if (model == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid enquiry request. Please provide valid enquiry details."
                });
            }

            try
            {
                using SqlConnection con =
                    new SqlConnection(GetConnectionString());

                using SqlCommand cmd =
                    new SqlCommand("SP_ManageEnquiry", con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("@FullName", SqlDbType.NVarChar, 100)
                    .Value = model.FullName;

                cmd.Parameters.Add("@Mobile", SqlDbType.NVarChar, 10)
                    .Value = model.Mobile;

                cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150)
                    .Value = (object?)model.Email ?? DBNull.Value;

                cmd.Parameters.Add("@Service", SqlDbType.NVarChar, 100)
                    .Value = model.Service;

                cmd.Parameters.Add("@FromLocation", SqlDbType.NVarChar, 150)
                    .Value = (object?)model.FromLocation ?? DBNull.Value;

                cmd.Parameters.Add("@ToLocation", SqlDbType.NVarChar, 150)
                    .Value = (object?)model.ToLocation ?? DBNull.Value;

                cmd.Parameters.Add("@TravelDate", SqlDbType.Date)
                    .Value = model.TravelDate.HasValue
                        ? model.TravelDate.Value.Date
                        : DBNull.Value;

                cmd.Parameters.Add("@Passengers", SqlDbType.NVarChar, 20)
                    .Value = (object?)model.Passengers ?? DBNull.Value;

                cmd.Parameters.Add("@TripDetails", SqlDbType.NVarChar, -1)
                    .Value = (object?)model.TripDetails ?? DBNull.Value;

                await con.OpenAsync();

                object? result = await cmd.ExecuteScalarAsync();

                int enquiryId = Convert.ToInt32(result);

                return Ok(new
                {
                    success = true,
                    message = "Your enquiry has been submitted successfully. Our team will contact you shortly.",
                    enquiryId = enquiryId
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);

                return StatusCode(500, new
                {
                    success = false,
                    message = "We were unable to submit your enquiry due to a technical issue. Please try again later."
                });
            }
        }


        // ==========================================
        // 2. GET ALL ENQUIRIES
        // GET: api/Enquiry
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> GetEnquiries()
        {
            try
            {
                var enquiries = new List<object>();

                using SqlConnection con =
                    new SqlConnection(GetConnectionString());

                string query = @"
                    SELECT EnquiryId, FullName, Mobile, Email, Service,
                           FromLocation, ToLocation, TravelDate,
                           Passengers, TripDetails, CreatedDate
                    FROM EnquiryMaster
                    ORDER BY EnquiryId DESC";

                using SqlCommand cmd = new SqlCommand(query, con);

                await con.OpenAsync();

                using SqlDataReader reader =
                    await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    enquiries.Add(new
                    {
                        EnquiryId = reader["EnquiryId"],

                        FullName = reader["FullName"],

                        Mobile = reader["Mobile"],

                        Email = reader["Email"] == DBNull.Value
                            ? null : reader["Email"].ToString(),

                        Service = reader["Service"],

                        FromLocation = reader["FromLocation"] == DBNull.Value
                            ? null : reader["FromLocation"].ToString(),

                        ToLocation = reader["ToLocation"] == DBNull.Value
                            ? null : reader["ToLocation"].ToString(),

                        TravelDate = reader["TravelDate"] == DBNull.Value
                            ? null : reader["TravelDate"],

                        Passengers = reader["Passengers"] == DBNull.Value
                            ? null : reader["Passengers"].ToString(),

                        TripDetails = reader["TripDetails"] == DBNull.Value
                            ? null : reader["TripDetails"].ToString(),

                        CreatedDate = reader["CreatedDate"]
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Enquiries retrieved successfully.",
                    data = enquiries
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);

                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to retrieve enquiries at this time. Please try again later."
                });
            }
        }


        // ==========================================
        // 3. UPDATE ENQUIRY
        // PUT: api/Enquiry/{id}
        // ==========================================

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateEnquiry(
            int id,
            [FromBody] EnquiryModel model)
        {
            if (id <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please provide a valid enquiry ID."
                });
            }

            if (model == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid request. Please provide valid enquiry details."
                });
            }

            try
            {
                using SqlConnection con =
                    new SqlConnection(GetConnectionString());

                string query = @"
                    UPDATE EnquiryMaster
                    SET FullName = @FullName,
                        Mobile = @Mobile,
                        Email = @Email,
                        Service = @Service,
                        FromLocation = @FromLocation,
                        ToLocation = @ToLocation,
                        TravelDate = @TravelDate,
                        Passengers = @Passengers,
                        TripDetails = @TripDetails
                    WHERE EnquiryId = @EnquiryId";

                using SqlCommand cmd = new SqlCommand(query, con);

                cmd.Parameters.Add("@EnquiryId", SqlDbType.Int)
                    .Value = id;

                cmd.Parameters.Add("@FullName", SqlDbType.NVarChar, 100)
                    .Value = model.FullName;

                cmd.Parameters.Add("@Mobile", SqlDbType.NVarChar, 10)
                    .Value = model.Mobile;

                cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150)
                    .Value = (object?)model.Email ?? DBNull.Value;

                cmd.Parameters.Add("@Service", SqlDbType.NVarChar, 100)
                    .Value = model.Service;

                cmd.Parameters.Add("@FromLocation", SqlDbType.NVarChar, 150)
                    .Value = (object?)model.FromLocation ?? DBNull.Value;

                cmd.Parameters.Add("@ToLocation", SqlDbType.NVarChar, 150)
                    .Value = (object?)model.ToLocation ?? DBNull.Value;

                cmd.Parameters.Add("@TravelDate", SqlDbType.Date)
                    .Value = model.TravelDate.HasValue
                        ? model.TravelDate.Value.Date
                        : DBNull.Value;

                cmd.Parameters.Add("@Passengers", SqlDbType.NVarChar, 20)
                    .Value = (object?)model.Passengers ?? DBNull.Value;

                cmd.Parameters.Add("@TripDetails", SqlDbType.NVarChar, -1)
                    .Value = (object?)model.TripDetails ?? DBNull.Value;

                await con.OpenAsync();

                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "The requested enquiry could not be found."
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Enquiry details updated successfully.",
                    enquiryId = id
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);

                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to update the enquiry due to a technical issue. Please try again later."
                });
            }
        }


        // ==========================================
        // 4. DELETE ENQUIRY
        // DELETE: api/Enquiry/{id}
        // ==========================================

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteEnquiry(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please provide a valid enquiry ID."
                });
            }

            try
            {
                using SqlConnection con =
                    new SqlConnection(GetConnectionString());

                string query = @"
                    DELETE FROM EnquiryMaster
                    WHERE EnquiryId = @EnquiryId";

                using SqlCommand cmd = new SqlCommand(query, con);

                cmd.Parameters.Add("@EnquiryId", SqlDbType.Int)
                    .Value = id;

                await con.OpenAsync();

                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "The requested enquiry could not be found."
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Enquiry deleted successfully.",
                    enquiryId = id
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);

                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to delete the enquiry due to a technical issue. Please try again later."
                });
            }
        }
    }
}
