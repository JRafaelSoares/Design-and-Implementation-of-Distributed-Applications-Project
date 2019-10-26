using System;
using System.Collections.Generic;

namespace MSDAD
{
    namespace Server
    {
        class Room
        {
            private string Name { get; }

            private uint Capacity { get; }

            //FIXME Make only date;
            private HashSet<DateTime> Bookings { get; }

            public Room(string name, uint capacity)
            {
                this.Name = name;
                this.Capacity = capacity;
                this.Bookings = new HashSet<DateTime>();
            }

            //Set Methods
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
                    return r.Name == this.Name;
                }
            }

            public override int GetHashCode()
            {
                return this.Name.GetHashCode();
            }

        }

        class Location
        {
            private string Name { get; }

            private HashSet<Room> Rooms { get; }

            public Location(string name, HashSet<Room> rooms)
            {
                this.Name = name;
                this.Rooms = rooms;
            }
            public Location(string name)
            {
                this.Name = name;
                this.Rooms = new HashSet<Room>();
            }
        }

    }
}
