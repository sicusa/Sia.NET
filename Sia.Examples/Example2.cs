namespace Sia.Examples;

using Sia;

public static class Example2
{
    public record struct Name(string Value)
    {
        public static implicit operator Name(string value)
            => new(value);
    }

    public record struct HP(
        int Value, int Maximum, int AutoRecoverRate)
    {
        public class Damage : PropertyCommand<Damage, int>
        {
            public override void Execute(in EntityRef target)
                => target.Get<HP>().Value -= Value;
        }

        public HP() : this(100, 100, 0) {}
    }

    public class HPAutoRecoverSystem : SystemBase
    {
        public HPAutoRecoverSystem()
        {
            Matcher = Matchers.From<TypeUnion<HP>>();
        }

        public override void Execute(World world, Scheduler scheduler, in EntityRef entity)
        {
            ref var hp = ref entity.Get<HP>();

            if (hp.Value < hp.Maximum) {
                hp.Value = Math.Min(hp.Value + hp.AutoRecoverRate, hp.Maximum);
                Console.WriteLine("血量已自动回复。");
            }
            else {
                Console.WriteLine("血量已满，未自动回复。");
            }
        }
    }

    public class DamageDisplaySystem : SystemBase
    {
        public DamageDisplaySystem()
        {
            Matcher = Matchers.From<TypeUnion<HP, Name>>();
            Trigger = new EventUnion<HP.Damage>();
        }

        public override void Execute(World world, Scheduler scheduler, in EntityRef entity)
        {
            Console.WriteLine($"[{entity.Get<Name>().Value}] 受到攻击！");
        }
    }

    public record struct Player(Name Name, HP HP)
    {
        public static EntityRef CreateResilient(string name)
            => EntityFactory<Player>.Hash.Create(new() {
                Name = name,
                HP = new() {
                    AutoRecoverRate = 10
                }
            });
    }

    public static void Run()
    {
        var world = new World();
        var scheduler = new Scheduler();

        new DamageDisplaySystem().Register(world, scheduler);
        new HPAutoRecoverSystem().Register(world, scheduler);

        var player = Player.CreateResilient("玩家");
        world.Add(player);

        ref var hp = ref player.Get<HP>();

        Console.WriteLine("HP: " + hp.Value);
        scheduler.Tick();

        world.Modify(player, HP.Damage.Create(50));
        Console.WriteLine("HP: " + hp.Value);
        scheduler.Tick();
        Console.WriteLine("HP: " + hp.Value);
    }
}