using CartOrderApi.Data;
using CartOrderApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CartOrderApi.Controllers
{
    [ApiController]
    [Route("api/orders")]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public OrderController(AppDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        private int UserId
        {
            get
            {
                var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("id");
                return userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
            }
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout()
        {

            var cartItems = await _context.Carts
                .Where(c => c.UserId == UserId)
                .Include(c => c.Product)
                .ToListAsync();

            if (!cartItems.Any())
                return BadRequest("Корзина пуста. Добавьте товары перед оформлением заказа.");

            var totalPrice = cartItems.Sum(c => c.Product.Price * c.Quantity);

            // Создать заказ
            var order = new Order
            {
                UserId = UserId,
                TotalPrice = totalPrice,
                Status = "pending"
            };

            await _context.Orders.AddAsync(order);


            foreach (var cartItem in cartItems)
            {
                cartItem.Product.Stock -= cartItem.Quantity;
                _context.Carts.Remove(cartItem);
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                OrderId = order.Id,
                TotalPrice = order.TotalPrice,
                Status = order.Status
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetUserOrders()
        {
            var orders = await _context.Orders
                .Where(o => o.UserId == UserId)
                .ToListAsync();

            return Ok(orders.Select(o => new
            {
                o.Id,
                o.TotalPrice,
                o.Status
            }));
        }

        [HttpGet("{orderId}")]
        public async Task<IActionResult> GetOrderDetails(int orderId)
        {
            var order = await _context.Orders
                .Where(o => o.Id == orderId && o.UserId == UserId)
                .FirstOrDefaultAsync();

            if (order == null)
                return NotFound("Заказ не найден.");

            return Ok(new
            {
                order.Id,
                order.TotalPrice,
                order.Status
            });
        }

        [HttpPut("{orderId}")]
        [Authorize]
        //[Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status)
        {
            var order = await _context.Orders.FindAsync(orderId);

            if (order == null)
                return NotFound("Заказ не найден.");

            order.Status = status;
            await _context.SaveChangesAsync();

            return Ok(new { order.Id, order.Status });
        }
    }


}