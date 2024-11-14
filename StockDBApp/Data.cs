using Microsoft.EntityFrameworkCore;
using StockDBApp.Models;

namespace StockDBApp.Data
{
    public class StockContext : DbContext
    {
        public DbSet<Stock> Stocks { get; set; }
        public DbSet<Price> Prices { get; set; }
        public DbSet<TodaysCondition> TodaysConditions { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Замените строку подключения на вашу
            optionsBuilder.UseSqlServer("Server=localhost,1433;Database=StockDb;User Id=sa;Password=Strong@Passw0rd;TrustServerCertificate=True");
        }
    }
}
