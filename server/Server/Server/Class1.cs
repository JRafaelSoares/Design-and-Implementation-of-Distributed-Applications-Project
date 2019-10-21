using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    class Room
    {
        private string name { get; }

        //FIXME make capacity be always positive
        private int capacity { get; }

        //FIXME Make only date;
        private HashSet<DateTime> bookings;

        public Room(string name, int capacity)
        {
            this.name = name;
            this.capacity = capacity;
            this.bookings = new HashSet<DateTime>();
        }

        public override bool Equals(Object obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                Room r = (Room)obj;
                return r.name == this.name;
            }
        }

    }

    class Location
    {
        private string name;

        private HashSet<Room> rooms;

        public Location(string name, HashSet<Room> rooms)
        {
            this.name = name;
            this.rooms = rooms;
        }
        public Location(string name)
        {
            this.name = name;
            this.rooms = new HashSet<Room>();
        }
    }
}
