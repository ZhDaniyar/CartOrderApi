using CartOrderApi.Data;
using CartOrderApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CartOrderApi.Controllers
{
    [ApiController]
    [Route("api/cart")]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CartController(AppDbContext context, IHttpContextAccessor httpContextAccessor)
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

        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity)
        {
            if (quantity <= 0)
                return BadRequest("Количество должно быть больше нуля.");

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
                return NotFound("Продукт не найден.");

            if (product.Stock < quantity)
                return BadRequest("Недостаточно товара на складе.");

            // Находим корзину пользователя по UserId
            var cartItem = await _context.Carts
                .FirstOrDefaultAsync(c => c.UserId == UserId && c.ProductId == productId);

            if (cartItem == null)
            {
                cartItem = new Cart
                {
                    UserId = UserId,
                    ProductId = productId,
                    Quantity = quantity
                };
                await _context.Carts.AddAsync(cartItem);
            }
            else
            {
                cartItem.Quantity += quantity;
            }

            product.Stock -= quantity;

            var user = await _context.Users.FindAsync(UserId);
            var userEmail = user?.Email;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                cartItem.Id,
                cartItem.ProductId,
                cartItem.Quantity,
                product.Name,
                product.Price,
                Total = cartItem.Quantity * product.Price,
                User = userEmail
            });
        }

        [HttpDelete("{productId}")]
        public async Task<IActionResult> RemoveFromCart(int productId)
        {
            var cartItem = await _context.Carts
                .FirstOrDefaultAsync(c => c.UserId == UserId && c.ProductId == productId);

            if (cartItem == null)
                return NotFound("Продукт в корзине не найден.");

            var product = await _context.Products.FindAsync(productId);
            if (product != null)
            {
                product.Stock += cartItem.Quantity;
            }

            _context.Carts.Remove(cartItem);
            await _context.SaveChangesAsync();

            return Ok("Продукт удалён из корзины.");
        }

        [HttpPut("{productId}")]
        public async Task<IActionResult> UpdateCart(int productId, int quantity)
        {
            if (quantity <= 0)
                return BadRequest("Количество должно быть больше нуля.");

            var cartItem = await _context.Carts
                .FirstOrDefaultAsync(c => c.UserId == UserId && c.ProductId == productId);

            if (cartItem == null)
                return NotFound("Продукт в корзине не найден.");

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
                return NotFound("Продукт не найден.");

            var difference = quantity - cartItem.Quantity;
            if (difference > 0 && product.Stock < difference)
                return BadRequest("Недостаточно товара на складе.");

            product.Stock -= difference;
            cartItem.Quantity = quantity;

            await _context.SaveChangesAsync();
            return Ok(cartItem);
        }

        [HttpGet]
        public async Task<IActionResult> GetCart()
        {
            var cartItems = await _context.Carts
                .Where(c => c.UserId == UserId)
                .Include(c => c.Product)
                .ToListAsync();

            return Ok(cartItems.Select(c => new
            {
                c.Id,
                c.ProductId,
                c.Product.Name,
                c.Product.Price,
                c.Quantity,
                Total = c.Quantity * c.Product.Price
            }));
        }
    }
}