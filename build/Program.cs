namespace Build;
using Cake.Frosting;

public static class Program
{
    public static int Main(string[] args) =>
        new CakeHost()
            .UseContext<BuildContext>()
            .Run(args);
}
