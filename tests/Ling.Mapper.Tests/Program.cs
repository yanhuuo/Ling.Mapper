// See https://aka.ms/new-console-template for more information
using Ling.Mapper;
using Ling.Mapper.Tests;
using TestConsole;
using System;
using System.Diagnostics;

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║         Ling.Mapper v2 - Comprehensive Test Suite            ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// 初始化 Mapper 配置
InitializeMapper();

// 🔄 循环测试菜单
while (true)
{
    // 显示测试菜单
    ShowTestMenu();

    var choice = Console.ReadLine();

    switch (choice)
    {
        case "1":
            RunBasicTests();
            break;
        case "2":
            RunAdvancedTests();
            break;
        case "3":
            RunPerformanceTests();
            break;
        case "4":
            RunStressTests();
            break;
        case "5":
            RunAutoInitializeTest();
            break;
        case "6":
            RunCollectionAutoDetectionTest();
            break;
        case "7":
            RunAdaptOptionsFlexibleTest();
            break;
        case "8":
            RunDefaultFlexibleOptionTest();
            break;
        case "9":
            RunNestedPropertyMappingTest();
            break;
        case "l":
        case "L":
            RunListConversionTest();
            break;
        case "d":
        case "D":
            LongToDateTimeDemo.Run();
            break;
        case "c":
        case "C":
            RunCircularReferenceTest();
            break;
        case "0":
            RunAllTests();
            break;
        case "q":
        case "Q":
            Console.WriteLine("\n👋 感谢使用 Ling.Mapper 测试套件！");
            return;
        default:
            Console.WriteLine("❌ 无效选择，请重新输入");
            break;
    }

    Console.WriteLine("\n" + new string('─', 60));
    // 直接返回测试菜单，移除等待输入的交互提示
    Console.WriteLine();
}

// ============ 初始化方法 ============

void InitializeMapper()
{
    Console.WriteLine("📦 初始化 Mapper 配置...");

    // 注册 JSON 转换器
    TypeConverterRegistry.RegisterJson<ExtraInfoModel>();

    // 配置 Mapper
    var cfg = new MapperConfiguration();
    cfg.AddProfile(new ActivityProfile());
    cfg.AddProfile(new CustomerDemoProfile());
    cfg.AddProfile(new UserProfile());
    cfg.AddProfile(new NullableTypeProfile());
    cfg.ConfigureConventions(opt =>
    {
        opt.CaseInsensitiveNameMatch = true;
    });

    var mapper = cfg.CreateMapper();
    MapperProvider.SetCurrent(mapper);

    Console.WriteLine("✅ Mapper 配置完成\n");
}

void ShowTestMenu()
{
    Console.WriteLine("请选择测试类型：");
    Console.WriteLine("  1  - 基础功能测试 (Basic Tests)");
    Console.WriteLine("  2  - 高级功能测试 (Advanced Tests)");
    Console.WriteLine("  3  - 性能基准测试 (Performance Tests)");
    Console.WriteLine("  4  - 压力测试 (Stress Tests)");
    Console.WriteLine("  5  - 自动初始化测试 (Auto Initialize Test)");
    Console.WriteLine("  6  - 集合自动识别测试 (Collection Auto Detection)");
    Console.WriteLine("  7  - AdaptOptions FlexibleOption 测试");
    Console.WriteLine("  8  - 默认 FlexibleOption 测试");
    Console.WriteLine("  9  - 嵌套属性映射测试 (A.B.C.D)");
    Console.WriteLine("  l  - List 类型转换测试 (List Conversion)");
    Console.WriteLine("  d  - long <-> DateTime 映射示例 (LongToDateTime Demo)");
    Console.WriteLine("  c  - 循环引用详细测试 (Circular Reference)");
    Console.WriteLine("  0  - 运行所有测试 (Run All Tests)");
    Console.WriteLine("  q  - 退出 (Exit)");
    Console.Write("\n选择 (1-9/l/d/c/0/q): ");
}

// ============ 测试套件 ============

void RunBasicTests()
{
    Console.WriteLine("\n╔═══════════════════════════════════════╗");
    Console.WriteLine("║      基础功能测试 (Basic Tests)        ║");
    Console.WriteLine("╚═══════════════════════════════════════╝\n");

    var sw = Stopwatch.StartNew();

    // 1. 基本映射测试
    BasicMappingTest.Run();

    // 2. 集合映射测试
    AdaptListDemo.Run();

    // 3. 可空类型测试
    NullableTypeDemo.Run();

    // 4. 枚举转换测试
    EnumConversionDemo.Run();

    // 5. AdaptOptions 测试
    AdaptOptionsDemo.Run();

    // 6. Adapt 扩展方法测试
    AdaptExtensionsTest.Run();

    sw.Stop();
    Console.WriteLine($"\n✅ 基础测试完成，耗时: {sw.ElapsedMilliseconds} ms\n");
}

void RunAdvancedTests()
{
    Console.WriteLine("\n╔═══════════════════════════════════════╗");
    Console.WriteLine("║     高级功能测试 (Advanced Tests)      ║");
    Console.WriteLine("╚═══════════════════════════════════════╝\n");

    var sw = Stopwatch.StartNew();

    // 1. 复杂对象映射测试
    ComplexObjectMappingTest.Run();

    // 2. 嵌套集合映射测试
    NestedCollectionTest.Run();

    // 3. 异常处理测试
    ExceptionHandlingTest.Run();

    // 4. Mapper v2 验证测试
    MapperV2ValidationTests.Run();

    // 5. 循环引用测试
    CircularReferenceTest.Run();

    // 6. 多层嵌套测试
    DeepNestingTest.Run();

    // 7. StackOverflow 修复验证测试（重要！）
    StackOverflowFixTest.Run();

    sw.Stop();
    Console.WriteLine($"\n✅ 高级测试完成，耗时: {sw.ElapsedMilliseconds} ms\n");
}

void RunAutoInitializeTest()
{
    Console.WriteLine("\n╔═══════════════════════════════════════╗");
    Console.WriteLine("║   自动初始化测试 (Auto Initialize)     ║");
    Console.WriteLine("╚═══════════════════════════════════════╝\n");

    var sw = Stopwatch.StartNew();

    AutoMapperProviderTest.Run();

    sw.Stop();
    Console.WriteLine($"\n✅ 自动初始化测试完成，耗时: {sw.ElapsedMilliseconds} ms\n");
}

void RunCollectionAutoDetectionTest()
{
    Console.WriteLine("\n╔═══════════════════════════════════════════╗");
    Console.WriteLine("║  集合自动识别测试 (Collection Auto Detect) ║");
    Console.WriteLine("╚═══════════════════════════════════════════╝\n");

    var sw = Stopwatch.StartNew();

    CollectionAutoDetectionTest.Run();

    sw.Stop();
    Console.WriteLine($"\n✅ 集合自动识别测试完成，耗时: {sw.ElapsedMilliseconds} ms\n");
}

void RunAdaptOptionsFlexibleTest()
{
    Console.WriteLine("\n╔═══════════════════════════════════════════════╗");
    Console.WriteLine("║  AdaptOptions FlexibleOption 测试 🔥           ║");
    Console.WriteLine("╚═══════════════════════════════════════════════╝\n");

    var sw = Stopwatch.StartNew();

    AdaptOptionsFlexibleTest.Run();

    sw.Stop();
    Console.WriteLine($"\n✅ AdaptOptions 测试完成，耗时: {sw.ElapsedMilliseconds} ms\n");
}

void RunDefaultFlexibleOptionTest()
{
    Console.WriteLine("\n╔══════════════════════════════════════════════════╗");
    Console.WriteLine("║  默认 FlexibleOption 测试 (v2.4) ⭐              ║");
    Console.WriteLine("╚══════════════════════════════════════════════════╝\n");

    var sw = Stopwatch.StartNew();

    DefaultFlexibleOptionTest.Run();

    sw.Stop();
    Console.WriteLine($"\n✅ 默认 FlexibleOption 测试完成，耗时: {sw.ElapsedMilliseconds} ms\n");
}

void RunNestedPropertyMappingTest()
{
    Console.WriteLine("\n╔═══════════════════════════════════════════════════╗");
    Console.WriteLine("║  嵌套属性映射测试 (A.B.C.D) 🎯 NEW!             ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════╝\n");

    var sw = Stopwatch.StartNew();

    NestedPropertyMappingTest.Run();

    sw.Stop();
    Console.WriteLine($"\n✅ 嵌套属性映射测试完成，耗时: {sw.ElapsedMilliseconds} ms\n");
}

void RunListConversionTest()
{
    Console.WriteLine("\n╔═══════════════════════════════════════════════════╗");
    Console.WriteLine("║  List 类型转换测试 (List Conversion) 🔧 FIX!    ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════╝\n");

    var sw = Stopwatch.StartNew();

    ListConversionTest.Run();

    sw.Stop();
    Console.WriteLine($"\n✅ List 类型转换测试完成，耗时: {sw.ElapsedMilliseconds} ms\n");
}

void RunCircularReferenceTest()
{
    Console.WriteLine("\n╔═══════════════════════════════════════════════════╗");
    Console.WriteLine("║  循环引用详细测试 (Circular Reference) 🔄 FIX!  ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════╝\n");

    var sw = Stopwatch.StartNew();

    CircularReferenceDetailedTest.Run();

    sw.Stop();
    Console.WriteLine($"\n✅ 循环引用测试完成，耗时: {sw.ElapsedMilliseconds} ms\n");
}






void RunPerformanceTests()
{
    Console.WriteLine("\n╔═══════════════════════════════════════╗");
    Console.WriteLine("║    性能基准测试 (Performance Tests)    ║");
    Console.WriteLine("╚═══════════════════════════════════════╝\n");

    PerformanceBenchmarkTest.Run();
}

void RunStressTests()
{
    Console.WriteLine("\n╔═══════════════════════════════════════╗");
    Console.WriteLine("║       压力测试 (Stress Tests)         ║");
    Console.WriteLine("╚═══════════════════════════════════════╝\n");

    StressTest.Run();
}

void RunAllTests()
{
    Console.WriteLine("\n╔═══════════════════════════════════════╗");
    Console.WriteLine("║    运行所有测试 (Run All Tests)        ║");
    Console.WriteLine("╚═══════════════════════════════════════╝\n");

    var totalSw = Stopwatch.StartNew();

    RunBasicTests();
    RunAdvancedTests();
    RunAutoInitializeTest();
    RunCollectionAutoDetectionTest();
    RunAdaptOptionsFlexibleTest();
    RunDefaultFlexibleOptionTest();
    RunNestedPropertyMappingTest();
    RunPerformanceTests();
    RunStressTests();






    totalSw.Stop();

    Console.WriteLine("\n╔═══════════════════════════════════════╗");
    Console.WriteLine("║           测试总结 (Summary)           ║");
    Console.WriteLine("╚═══════════════════════════════════════╝");
    Console.WriteLine($"  总耗时: {totalSw.ElapsedMilliseconds} ms");
    Console.WriteLine($"  状态: ✅ 所有测试完成");
    Console.WriteLine();
}
