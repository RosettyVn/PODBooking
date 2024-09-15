﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.Entities
{
    public class BookingService
    {
        public int Id { get; set; }
        public int Quantity {  get; set; }
        public float Price {  get; set; }
        public int BookingId {  get; set; }
        public Booking Booking { get; set; }
        public int ServiceId {  get; set; }
        public Service Service { get; set; }

    }
}
