﻿using System;
using System.Collections.Generic;

namespace TorontoDaycares.Models
{
    public class Daycare
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Uri Uri { get; set; }
        public int WardNumber { get; set; }

        public string Address { get; set; }

        public List<DaycareProgram> Programs { get; set; }
    }
}