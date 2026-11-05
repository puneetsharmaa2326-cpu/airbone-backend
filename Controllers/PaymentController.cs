
using AirborneEnquiryApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace AirborneEnquiryApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly string _connectionString;

        public PaymentController(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Database connection string missing.");
        }

        // =========================================
        // 1. COMMON SQL PARAMETERS
        // =========================================

        private static void AddParameters(
            SqlCommand cmd,
            string action,
            int? id = null,
            PaymentRequest? request = null)
        {
            cmd.Parameters.Add("@Action", SqlDbType.NVarChar, 20)
                .Value = action;

            cmd.Parameters.Add("@PaymentId", SqlDbType.Int)
                .Value = (object?)id ?? DBNull.Value;

            cmd.Parameters.Add("@FullName", SqlDbType.NVarChar, 150)
                .Value = (object?)request?.FullName?.Trim() ?? DBNull.Value;

            cmd.Parameters.Add("@MobileNumber", SqlDbType.NVarChar, 20)
                .Value = (object?)request?.MobileNumber?.Trim() ?? DBNull.Value;

            cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150)
                .Value = (object?)request?.Email?.Trim() ?? DBNull.Value;

            cmd.Parameters.Add("@Service", SqlDbType.NVarChar, 100)
                .Value = (object?)request?.Service?.Trim() ?? DBNull.Value;

            cmd.Parameters.Add("@BookingReference", SqlDbType.NVarChar, 100)
                .Value = (object?)request?.BookingReference?.Trim() ?? DBNull.Value;

            var amountParameter = cmd.Parameters.Add(
                "@AmountPaid", SqlDbType.Decimal);

            amountParameter.Precision = 18;
            amountParameter.Scale = 2;
            amountParameter.Value =
                (object?)request?.AmountPaid ?? DBNull.Value;

            cmd.Parameters.Add("@UTRNumber", SqlDbType.NVarChar, 100)
                .Value = (object?)request?.UTRNumber?.Trim() ?? DBNull.Value;
        }

        // =========================================
        // 2. READ PAYMENT DATA
        // =========================================

        private static PaymentModel ReadPayment(
            SqlDataReader reader)
        {
            return new PaymentModel
            {
                PaymentId = Convert.ToInt32(
                    reader["PaymentId"]),

                FullName = Convert.ToString(
                    reader["FullName"]) ?? "",

                MobileNumber = Convert.ToString(
                    reader["MobileNumber"]) ?? "",

                Email = Convert.ToString(
                    reader["Email"]) ?? "",

                Service = Convert.ToString(
                    reader["Service"]) ?? "",

                BookingReference = Convert.ToString(
                    reader["BookingReference"]) ?? "",

                AmountPaid = Convert.ToDecimal(
                    reader["AmountPaid"]),

                UTRNumber = Convert.ToString(
                    reader["UTRNumber"]) ?? "",

                CreatedAt = Convert.ToDateTime(
                    reader["CreatedAt"])
            };
        }

        // =========================================
        // 3. GET PAYMENT LIST
        // =========================================

        private async Task<List<PaymentModel>> GetList(
            string action,
            int? id = null)
        {
            var list = new List<PaymentModel>();

            await using var con =
                new SqlConnection(_connectionString);

            await using var cmd =
                new SqlCommand("dbo.SP_ManagePayment", con);

            cmd.CommandType = CommandType.StoredProcedure;

            AddParameters(cmd, action, id);

            await con.OpenAsync();

            await using var reader =
                await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(ReadPayment(reader));
            }

            return list;
        }

        // =========================================
        // 4. CREATE PAYMENT
        // POST: api/Payment
        // =========================================

        [HttpPost]
        public async Task<IActionResult> CreatePayment(
            [FromBody] PaymentRequest request)
        {
            try
            {
                await using var con =
                    new SqlConnection(_connectionString);

                await using var cmd =
                    new SqlCommand("dbo.SP_ManagePayment", con);

                cmd.CommandType = CommandType.StoredProcedure;

                AddParameters(
                    cmd,
                    "INSERT",
                    request: request
                );

                await con.OpenAsync();

                await using var reader =
                    await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    throw new Exception(
                        "Payment was not saved.");
                }

                var savedPayment = ReadPayment(reader);

                return CreatedAtAction(
                    nameof(GetPaymentById),
                    new { id = savedPayment.PaymentId },
                    new
                    {
                        success = true,
                        message = "Payment saved successfully.",
                        data = savedPayment
                    });
            }
            catch (SqlException ex) when (ex.Number == 50001)
            {
                return Conflict(new
                {
                    success = false,
                    message = "This UTR number already exists."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to save payment.",
                    error = ex.Message
                });
            }
        }

        // =========================================
        // 5. GET ALL PAYMENTS
        // GET: api/Payment
        // =========================================

        [HttpGet]
        public async Task<IActionResult> GetAllPayments()
        {
            try
            {
                var data = await GetList("GETALL");

                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to fetch payments.",
                    error = ex.Message
                });
            }
        }

        // =========================================
        // 6. GET PAYMENT BY ID
        // GET: api/Payment/1
        // =========================================

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetPaymentById(int id)
        {
            if (id <= 0)
                return BadRequest("Invalid PaymentId.");

            try
            {
                var data = await GetList("GETBYID", id);

                if (data.Count == 0)
                    return NotFound("Payment not found.");

                return Ok(data[0]);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to fetch payment.",
                    error = ex.Message
                });
            }
        }

        // =========================================
        // 7. UPDATE PAYMENT
        // PUT: api/Payment/1
        // =========================================

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdatePayment(
            int id,
            [FromBody] PaymentRequest request)
        {
            if (id <= 0)
                return BadRequest("Invalid PaymentId.");

            try
            {
                var existing = await GetList("GETBYID", id);

                if (existing.Count == 0)
                    return NotFound("Payment not found.");

                await using var con =
                    new SqlConnection(_connectionString);

                await using var cmd =
                    new SqlCommand("dbo.SP_ManagePayment", con);

                cmd.CommandType = CommandType.StoredProcedure;

                AddParameters(
                    cmd,
                    "UPDATE",
                    id,
                    request
                );

                await con.OpenAsync();

                await using var reader =
                    await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return NotFound("Payment update failed.");

                var updatedPayment = ReadPayment(reader);

                return Ok(new
                {
                    success = true,
                    message = "Payment updated successfully.",
                    data = updatedPayment
                });
            }
            catch (SqlException ex) when (ex.Number == 50001)
            {
                return Conflict(new
                {
                    success = false,
                    message = "This UTR number already exists."
                });
            }
            catch (SqlException ex) when (ex.Number == 50002)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Payment not found."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to update payment.",
                    error = ex.Message
                });
            }
        }

        // =========================================
        // 8. DELETE PAYMENT
        // DELETE: api/Payment/1
        // =========================================

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeletePayment(int id)
        {
            if (id <= 0)
                return BadRequest("Invalid PaymentId.");

            try
            {
                var existing = await GetList("GETBYID", id);

                if (existing.Count == 0)
                    return NotFound("Payment not found.");

                await using var con =
                    new SqlConnection(_connectionString);

                await using var cmd =
                    new SqlCommand("dbo.SP_ManagePayment", con);

                cmd.CommandType = CommandType.StoredProcedure;

                AddParameters(cmd, "DELETE", id);

                await con.OpenAsync();

                int rows = Convert.ToInt32(
                    await cmd.ExecuteScalarAsync());

                if (rows == 0)
                    return NotFound("Payment not found.");

                return Ok(new
                {
                    success = true,
                    message = "Payment deleted successfully."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to delete payment.",
                    error = ex.Message
                });
            }
        }
    }
}
