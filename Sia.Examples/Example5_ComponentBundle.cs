using System.Numerics;
using Sia;

namespace Sia_Examples
{
    namespace ComponentBundle
    {
        public partial record struct Position([Sia] Vector3 Value);
        public partial record struct Rotation([Sia] Quaternion Value);
        public partial record struct Scale([Sia] Vector3 Value);

        [SiaBakeEntity]
        public partial record struct Transform(Position Position, Rotation Rotation, Scale Scale);

        public readonly record struct ObjectId(int Value)
        {
            public static implicit operator ObjectId(int id)
                => new(id);
        }

        public record struct Name([Sia] string Value)
        {
            public static implicit operator Name(string name)
                => new(name);
        }

        [SiaBakeEntity]
        public partial record struct GameObject(Sid<ObjectId> Id, Name Name);

        public record struct HP([Sia] int Value);
        
        public static class TestObject
        {
            public static EntityRef Create(World world)
            {
                var transform = new Transform {
                    Position = new Position {
                        Value = Vector3.Zero
                    },
                    Rotation = new Rotation {
                        Value = Quaternion.Identity
                    },
                    Scale = new Scale {
                        Value = Vector3.One
                    }
                };
                var gameObject = new GameObject {
                    Id = new Sid<ObjectId>(0),
                    Name = "Test"
                };
                return world.CreateInArrayHost(HList.Create(new HP(100)))
                    .AddBundle(transform)
                    .AddBundle(gameObject);
            }
        }
    }

    public static partial class Example5_ComponentBundle
    {
        public static void Run(World world)
        {
            var entity = ComponentBundle.TestObject.Create(world);
            Console.WriteLine(entity.Get<ComponentBundle.Name>().Value);
            Console.WriteLine(entity.Get<ComponentBundle.HP>().Value);
            Console.WriteLine(entity.Get<ComponentBundle.Position>().Value);
            Console.WriteLine(entity.Get<ComponentBundle.Rotation>().Value);
            Console.WriteLine(entity.Get<ComponentBundle.Scale>().Value);
        }
    }
}