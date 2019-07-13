using System;
using System.Reflection;
using System.Reflection.Emit;

namespace PerfHub
{
    public static class DynamicTypeBuilder
    {
        private static int Counter { get; set; } = 0;

        private static AssemblyName MyAssemblyName { get; } = new AssemblyName("MyAssembly");

        private static AssemblyBuilder MyAssembly { get; } = AssemblyBuilder.DefineDynamicAssembly(MyAssemblyName, AssemblyBuilderAccess.Run);

        private static ModuleBuilder MainModule { get; } = MyAssembly.DefineDynamicModule("MainModule");

        public static Type CreateType()
        {
            return GetTypeBuilder($"MyType{Counter++}").CreateTypeInfo();
        }

        private static TypeBuilder GetTypeBuilder(string name)
          => MainModule.DefineType(name,
              TypeAttributes.Public |
              TypeAttributes.Class |
              TypeAttributes.AutoClass |
              TypeAttributes.AnsiClass |
              TypeAttributes.BeforeFieldInit |
              TypeAttributes.AutoLayout,
              null);
    }
}