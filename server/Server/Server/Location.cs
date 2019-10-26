using System;
using System.Collections.Generic;

namespace MSDAD
{
    namespace Server
    {
        class Room
        {
            public string Name { get; }

            public uint Capacity { get; }

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

            public bool IsBooked(DateTime time)
            {
                //Problem with contains probable >.>
                return Bookings.Contains(time);
            }

            public void AddBooking(DateTime time)
            {
                Bookings.Add(time);
            }
        }

        class Location
        {
            static readonly Dictionary<String, Location> Locations = new Dictionary<string, Location>();

            public string Name { get; }

            public List<Room> Rooms { get; }

            public Location(string name, List<Room> rooms)
            {
                this.Name = name;
                this.Rooms = rooms;
            }

            public Location(string name)
            {
                this.Name = name;
                this.Rooms = new List<Room>();
            }

            public List<Room> GetOrderedRooms()
            {
                Rooms.Sort((x, y) => x.Capacity.CompareTo(y.Capacity));
                return Rooms;
            }

            public void AddRoom(Room room)
            {
                Rooms.Add(room);
            }

            public override string ToString()
            {
                return this.Name;
            }

            public bool Equals(Location other)
            {
                return this.Name == other.Name;
            }

            public static Location FromName(String name)
            {
                return Locations[name];
            }

            public static void AddLocation(Location location)
            {
                Locations.Add(location.Name, location);
            }
        }

    }
}
