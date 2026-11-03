
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoryController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public CategoryController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection")
            );
        }

        // =========================================
        // 1. ADD CATEGORY
        // POST: api/Category
        // =========================================

        [HttpPost]
        public async Task<IActionResult> AddCategory(
            [FromBody] CategoryModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(model.CategoryName))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Category name is required."
                });
            }

            try
            {
                using SqlConnection con = GetConnection();

                using SqlCommand cmd = new SqlCommand(
                    "SP_ManageCategory", con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@Action", "INSERT");

                cmd.Parameters.AddWithValue(
                    "@CategoryName",
                    model.CategoryName.Trim()
                );

                cmd.Parameters.AddWithValue(
                    "@Description",
                    (object?)model.Description?.Trim() ?? DBNull.Value
                );

                await con.OpenAsync();

                using SqlDataReader reader =
                    await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Category added successfully.",

                        data = new
                        {
                            categoryId = Convert.ToInt32(
                                reader["CategoryId"]
                            ),

                            categoryName =
                                reader["CategoryName"].ToString(),

                            description =
                                reader["Description"] == DBNull.Value
                                ? null
                                : reader["Description"].ToString(),

                            createdDate =
                                reader["CreatedDate"] == DBNull.Value
                                ? (DateTime?)null
                                : Convert.ToDateTime(
                                    reader["CreatedDate"]
                                ),

                            isActive = Convert.ToBoolean(
                                reader["IsActive"]
                            )
                        }
                    });
                }

                return StatusCode(500, new
                {
                    success = false,
                    message = "Category could not be added."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // =========================================
        // 2. GET ALL ACTIVE CATEGORIES
        // GET: api/Category
        // =========================================

        [HttpGet]
        public async Task<IActionResult> GetAllCategories()
        {
            try
            {
                using SqlConnection con = GetConnection();

                using SqlCommand cmd = new SqlCommand(
                    "SP_ManageCategory", con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@Action", "GETALL");

                await con.OpenAsync();

                using SqlDataReader reader =
                    await cmd.ExecuteReaderAsync();

                List<CategoryModel> categories =
                    new List<CategoryModel>();

                while (await reader.ReadAsync())
                {
                    categories.Add(new CategoryModel
                    {
                        CategoryId = Convert.ToInt32(
                            reader["CategoryId"]
                        ),

                        CategoryName =
                            reader["CategoryName"].ToString() ?? "",

                        Description =
                            reader["Description"] == DBNull.Value
                            ? null
                            : reader["Description"].ToString(),

                        CreatedDate =
                            reader["CreatedDate"] == DBNull.Value
                            ? (DateTime?)null
                            : Convert.ToDateTime(
                                reader["CreatedDate"]
                            )
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = categories
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // =========================================
        // 3. GET CATEGORY BY ID
        // GET: api/Category/1
        // =========================================

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetCategoryById(int id)
        {
            try
            {
                using SqlConnection con = GetConnection();

                using SqlCommand cmd = new SqlCommand(
                    "SP_ManageCategory", con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@Action", "GETBYID");

                cmd.Parameters.AddWithValue("@CategoryId", id);

                await con.OpenAsync();

                using SqlDataReader reader =
                    await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Category not found."
                    });
                }

                var category = new CategoryModel
                {
                    CategoryId = Convert.ToInt32(
                        reader["CategoryId"]
                    ),

                    CategoryName =
                        reader["CategoryName"].ToString() ?? "",

                    Description =
                        reader["Description"] == DBNull.Value
                        ? null
                        : reader["Description"].ToString(),

                    CreatedDate =
                        reader["CreatedDate"] == DBNull.Value
                        ? (DateTime?)null
                        : Convert.ToDateTime(
                            reader["CreatedDate"]
                        )
                };

                return Ok(new
                {
                    success = true,
                    data = category
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // =========================================
        // 4. UPDATE CATEGORY
        // PUT: api/Category/1
        // =========================================

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateCategory(
            int id,
            [FromBody] CategoryModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(model.CategoryName))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Category name is required."
                });
            }

            try
            {
                using SqlConnection con = GetConnection();

                using SqlCommand cmd = new SqlCommand(
                    "SP_ManageCategory", con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@Action", "UPDATE");

                cmd.Parameters.AddWithValue("@CategoryId", id);

                cmd.Parameters.AddWithValue(
                    "@CategoryName",
                    model.CategoryName.Trim()
                );

                cmd.Parameters.AddWithValue(
                    "@Description",
                    (object?)model.Description?.Trim() ?? DBNull.Value
                );

                await con.OpenAsync();

                int affectedRows = Convert.ToInt32(
                    await cmd.ExecuteScalarAsync()
                );

                if (affectedRows == 0)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Category not found."
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Category updated successfully."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // =========================================
        // 5. DELETE CATEGORY (SOFT DELETE)
        // DELETE: api/Category/1
        // =========================================

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            try
            {
                using SqlConnection con = GetConnection();

                using SqlCommand cmd = new SqlCommand(
                    "SP_ManageCategory", con);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@Action", "DELETE");

                cmd.Parameters.AddWithValue("@CategoryId", id);

                await con.OpenAsync();

                int affectedRows = Convert.ToInt32(
                    await cmd.ExecuteScalarAsync()
                );

                if (affectedRows == 0)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Category not found."
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Category deleted successfully."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }
    }
}
