using System;
using Autofac;
using Autofac.Configuration;
using Autofac.Features.OwnedInstances;
using Whitebox.Containers.Autofac;

namespace ProfiledApplication
{
    class A
    {
    }

    class B
    {
        public B(A a)
        {
        }

        public D D { get; set; }
    }

    class C
    {
        public C(B b)
        {
        }
    }

    class D
    {        
    }

    class G<T,U>
    {        
    }

    class F
    {
        public F(A a)
        {
            
        }
    }

    class Program
    {
        static void Main()
        {
            Console.WriteLine("Started.");

            var builder = new ContainerBuilder();
            //builder.RegisterModule<WhiteboxProfilingModule>();
            builder.RegisterModule(new ConfigurationSettingsReader());
            builder.RegisterType<A>().SingleInstance();
            builder.RegisterType<B>().PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);
            builder.RegisterType<C>().WithMetadata("M", 42).WithMetadata("N", "B!");
            builder.RegisterType<D>().SingleInstance();
            builder.RegisterGeneric(typeof (G<,>));

            using (var container = builder.Build())
            {
                using (var ls1 = container.BeginLifetimeScope())
                {
                    var o1 = ls1.Resolve<C>();
                    Console.WriteLine("Resolved a {0}.", o1);
                }

                Console.WriteLine("Taking a nap...");
                System.Threading.Thread.Sleep(5000);

                using (var ls2 = container.BeginLifetimeScope())
                {
                    var o = ls2.Resolve<C>();
                    Console.WriteLine("Resolved a {0}.", o);

                    var g = ls2.Resolve<G<int, string>>();
                    Console.WriteLine("Resolved a {0}.", g);

                    var ov = ls2.Resolve<Owned<C>>();
                    Console.WriteLine("Resolved an {0}", ov);
                }

                using (var ls3 = container.BeginLifetimeScope("service", x =>
                {
                    var childModule = new ChildScopePipingProfilingModule(container.Resolve<WhiteboxProfilingModule>());
                    x.RegisterModule(childModule);
                    x.RegisterType<F>();
                }))
                {
                    var f = ls3.Resolve<F>();
                    Console.WriteLine("Resolved a {0}.", f);
                }        
            }

            Console.WriteLine("Done. Press any key...");
            Console.ReadKey(true);
        }
    }
}
