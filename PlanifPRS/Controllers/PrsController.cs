using Microsoft.AspNetCore.Mvc;
using PlanifPRS.Data;
using System.Linq;
using System.Threading.Tasks;

namespace PlanifPRS.Controllers
{
    [Route("Fiche/[controller]")]
    public class PrsController : Controller
    {
        private readonly PlanifPrsDbContext _context;

        public PrsController(PlanifPrsDbContext context)
        {
            _context = context;
        }

        [HttpGet("{id}")]
        public IActionResult Edit(int id)
        {
            return Redirect($"/Fiche/Edit/{id}");
        }
    }
}
