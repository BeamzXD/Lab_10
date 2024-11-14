using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StockDBApp.Models
{
    public class Stock
    {
        [Key]
        public string Ticker { get; set; }
        public ICollection<Price> Prices { get; set; }
        public TodaysCondition TodaysCondition { get; set; }
    }

    public class Price
    {
        [Key]
        public int PriceId { get; set; }
        public string Ticker { get; set; }
        [ForeignKey("Ticker")]
        public Stock Stock { get; set; }
        public DateTime Date { get; set; }
        public double AveragePrice { get; set; }
    }

    public class TodaysCondition
    {
        [Key]
        public int ConditionId { get; set; }
        public string Ticker { get; set; }
        [ForeignKey("Ticker")]
        public Stock Stock { get; set; }
        public string Condition { get; set; } // "Выросла" или "Упала"
        public DateTime Date { get; set; }
    }
}
