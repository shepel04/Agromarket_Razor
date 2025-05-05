using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Agromarket.Models
{
    public enum OrderStatus
    {
        Виконується,
        Виконано,
        Скасовано
    }

    public class Order
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Ім'я є обов'язковим")]
        [StringLength(50, ErrorMessage = "Ім'я не може бути довшим за 50 символів")]
        [Display(Name = "Ім'я")]
        public string CustomerName { get; set; }

        [Required(ErrorMessage = "Email є обов'язковим")]
        [EmailAddress(ErrorMessage = "Введіть коректний email")]
        [Display(Name = "Email")]
        public string CustomerEmail { get; set; }

        [Required(ErrorMessage = "Телефон є обов'язковим")]
        [Display(Name = "Телефон")]
        [RegularExpression(@"^(\+380\d{9}|0\d{9})$", ErrorMessage = "Формат телефону: +380xxxxxxxxx або 0xxxxxxxxx")]
        public string CustomerPhone { get; set; }

        [Required(ErrorMessage = "Адреса доставки є обов'язковою")]
        [StringLength(200, ErrorMessage = "Адреса не може бути довшою за 200 символів")]
        [Display(Name = "Адреса доставки")]
        public string DeliveryAddress { get; set; }

        [Display(Name = "Дата замовлення")]
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Статус замовлення")]
        public OrderStatus Status { get; set; } = OrderStatus.Виконується; 

        public decimal TotalAmount { get; set; }
        public List<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public bool PreorderPaid { get; set; }
    }
}