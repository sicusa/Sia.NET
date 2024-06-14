﻿namespace Sia.CodeGenerators;

internal partial class SiaBundleGenerator
{
    public static readonly string SiaBundleAttributeName = "Sia.SiaBundleAttribute";
    public static readonly string SiaBundleAttributeSource =
        $$"""
        // <auto-generated/>
        #nullable enable

        namespace Sia;

        [{{Common.GeneratedCodeAttribute}}]
        [global::System.AttributeUsage(global::System.AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
        internal sealed class SiaBundleAttribute : global::System.Attribute;
        """;
}