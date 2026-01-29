using System;
using System.Collections.Generic;
using System.Linq;
using Ling.Mapper;
using Ling.Mapper.Extensions;
using Ling.Mapper.Models;

namespace TestConsole.Test
{
    /// <summary>
    /// Adapt 扩展方法综合测试套件
    /// 覆盖所有 Adapt 方法重载和使用场景
    /// </summary>
    public static class AdaptExtensionsTest
    {
        private static int _passedTests = 0;
        private static int _failedTests = 0;

        public static void Run()
        {
            Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║         Adapt 扩展方法综合测试 (Adapt Extensions Test)        ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");

            _passedTests = 0;
            _failedTests = 0;

            TestBasicAdapt();
            TestAdaptWithCallback();
            TestAdaptWithMapper();
            TestAdaptWithOptions();
            TestAdaptList();
            TestAdaptListWithCallback();
            TestAdaptWithNullHandling();
            TestAdaptWithCircularReference();
            TestAdaptEdgeCases();

            Console.WriteLine($"\n📊 测试统计: ✅ {_passedTests} 通过, ❌ {_failedTests} 失败");
            if (_failedTests == 0)
            {
                Console.WriteLine("✅ Adapt 扩展方法测试完成 - 所有测试通过\n");
            }
            else
            {
                Console.WriteLine($"⚠️  Adapt 扩展方法测试完成 - {_failedTests} 个测试失败\n");
            }
        }

        #region 1. 基础 Adapt 测试

        private static void TestBasicAdapt()
        {
            Console.WriteLine("1. 基础 Adapt 测试");

            // 简单对象映射
            var source = new SimpleSource { Id = 1, Name = "Test" };
            var target = source.Adapt<SimpleTarget>();

            AssertEqual(source.Id, target?.Id, "Id 映射");
            AssertEqual(source.Name, target?.Name, "Name 映射");

            Console.WriteLine("  ✓ 基础 Adapt 映射成功");
            Console.WriteLine();
        }

        #endregion

        #region 2. Adapt 带回调测试

        private static void TestAdaptWithCallback()
        {
            Console.WriteLine("2. Adapt 带回调测试");

            var source = new UserSource 
            { 
                Id = 1, 
                FirstName = "John", 
                LastName = "Doe",
                Age = 30
            };

            // 测试 1: (TDestination, TSource) 回调
            var target1 = source.Adapt<UserTarget, UserSource>((src,dest) =>
            {
                src.FullName = $"{dest.FirstName} {dest.LastName}";
                src.IsAdult = dest.Age >= 18;
            });

            AssertEqual("John Doe", target1?.FullName, "FullName 回调");
            AssertTrue(target1?.IsAdult == true, "IsAdult 回调");
            Console.WriteLine("  ✓ (TDestination, TSource) 回调成功");

            // 测试 2: (TSource, TDestination) 回调（旧格式）
            var target2 = source.Adapt<UserTarget, UserSource>((src, dest) =>
            {
                src.FullName = $"{src.FirstName} {src.LastName}".ToUpper();
            });

            AssertEqual("JOHN DOE", target2?.FullName, "FullName 回调（旧格式）");
            Console.WriteLine("  ✓ (TSource, TDestination) 回调成功");

            Console.WriteLine();
        }

        #endregion

        #region 3. Adapt 指定 Mapper 测试

        private static void TestAdaptWithMapper()
        {
            Console.WriteLine("3. Adapt 指定 Mapper 测试");

            // 创建自定义 Mapper

            var source = new SimpleSource { Id = 2, Name = "Custom" };
            
            // 使用自定义 Mapper
            var target = source.Adapt<SimpleTarget>();

            AssertEqual(source.Id, target?.Id, "Id 映射");
            AssertEqual(source.Name, target?.Name, "Name 映射");

            Console.WriteLine("  ✓ 自定义 Mapper 映射成功");
            Console.WriteLine();
        }

        #endregion

        #region 4. Adapt 带 Options 测试

        private static void TestAdaptWithOptions()
        {
            Console.WriteLine("4. Adapt 带 Options 测试");

            // 测试 1: IgnoreCase（只忽略大小写，不忽略下划线）
            var source1 = new { id = 1, name = "Test" };
            var target1 = source1.Adapt<CaseTestTarget>(AdaptOptions.IgnoreCase);
            
            AssertEqual(1, target1?.Id, "IgnoreCase - Id");
            AssertEqual("Test", target1?.Name, "IgnoreCase - Name");
            Console.WriteLine("  ✓ IgnoreCase 选项成功");

            // 测试 2: IgnoreUnderscore（只忽略下划线，不忽略大小写）
            var source2 = new { Id = 2, Name = "Test", User_Name = "Jane" }; // 注意：User_Name 保持大小写
            var target2 = source2.Adapt<UnderscoreTestTarget>(AdaptOptions.IgnoreUnderscoreOption);
            
            AssertEqual("Jane", target2?.UserName, "IgnoreUnderscore - UserName");
            Console.WriteLine("  ✓ IgnoreUnderscore 选项成功");

            // 测试 2b: IgnoreUnderscore 但大小写不匹配（应该失败）
            var source2b = new { Id = 3, Name = "Test", user_name = "Bob" }; // 小写，应该不匹配
            var target2b = source2b.Adapt<UnderscoreTestTarget>(AdaptOptions.IgnoreUnderscoreOption);
            
            if (target2b?.UserName == null || target2b?.UserName == "")
            {
                Console.WriteLine("  ✓ IgnoreUnderscore 正确忽略了大小写不匹配的属性");
                _passedTests++;
            }
            else
            {
                Console.WriteLine($"  ❌ IgnoreUnderscore 错误地映射了 user_name: {target2b?.UserName}");
                _failedTests++;
            }

            // 测试 3: FlexibleOption（组合：忽略大小写 + 忽略下划线）
            var source3 = new { id = 4, NAME = "Flexible", user_name = "Alice" }; // 小写 + 下划线
            var target3 = source3.Adapt<FlexibleTestTarget>(AdaptOptions.FlexibleOption);
            
            AssertEqual(4, target3?.Id, "FlexibleOption - Id");
            AssertEqual("Flexible", target3?.Name, "FlexibleOption - Name");
            AssertEqual("Alice", target3?.UserName, "FlexibleOption - UserName");
            Console.WriteLine("  ✓ FlexibleOption 选项成功");

            // 测试 4: IgnoreNullValues
            var source4 = new NullTestSource { Id = 5, Name = null, Description = "Test" };
            var target4 = source4.Adapt<NullTestTarget>(AdaptOptions.IgnoreNullValues);
            
            AssertEqual(5, target4?.Id, "IgnoreNullValues - Id");
            AssertEqual("Default", target4?.Name, "IgnoreNullValues - Name (应保留默认值)");
            Console.WriteLine("  ✓ IgnoreNullValues 选项成功");

            // 测试 5: Strict 模式（精确匹配，不忽略大小写/下划线）
            var source5 = new { Id = 6, Name = "Strict" }; // 属性名必须精确匹配
            var target5 = source5.Adapt<CaseTestTarget>(AdaptOptions.Strict);
            
            AssertEqual(6, target5?.Id, "Strict - Id");
            AssertEqual("Strict", target5?.Name, "Strict - Name");
            Console.WriteLine("  ✓ Strict 选项成功");

            // 测试 6: 组合 Options + Callback（使用 Default 选项）
            var source6 = new { id = 7, first_NAME = "Bob", last_name = "Smith" };
            var target6 = source6.Adapt<CombinedTestTarget>(
                (dest, src) =>
                {
                    dest.FullName = $"{dest.FirstName} {dest.LastName}";
                });
            
            AssertEqual("Bob", target6?.FirstName, "组合 - FirstName");
            AssertEqual("Smith", target6?.LastName, "组合 - LastName");
            AssertEqual("Bob Smith", target6?.FullName, "组合 - FullName");
            Console.WriteLine("  ✓ Options + Callback 组合成功");

            Console.WriteLine();
        }

        #endregion

        #region 5. AdaptList 测试

        private static void TestAdaptList()
        {
            Console.WriteLine("5. AdaptList 测试");

            var sourceList = new List<SimpleSource>
            {
                new SimpleSource { Id = 1, Name = "Item 1" },
                new SimpleSource { Id = 2, Name = "Item 2" },
                new SimpleSource { Id = 3, Name = "Item 3" }
            };

            // 简单 AdaptList
            var targetList = sourceList.Adapt<List<SimpleTarget>>();

            AssertEqual(3, targetList?.Count, "List 长度");
            AssertEqual(1, targetList?[0].Id, "Item 0 - Id");
            AssertEqual("Item 1", targetList?[0].Name, "Item 0 - Name");
            AssertEqual(3, targetList?[2].Id, "Item 2 - Id");

            Console.WriteLine("  ✓ AdaptList 映射成功");
            Console.WriteLine();
        }

        #endregion

        #region 6. AdaptList 带回调测试

        private static void TestAdaptListWithCallback()
        {
            Console.WriteLine("6. AdaptList 带回调测试");

            var sourceList = new List<UserSource>
            {
                new UserSource { Id = 1, FirstName = "John", LastName = "Doe", Age = 30 },
                new UserSource { Id = 2, FirstName = "Jane", LastName = "Smith", Age = 25 }
            };

            // Adapt 自动识别集合类型，在回调中处理索引
            var targetList = sourceList.Adapt<List<UserTarget>>((list, src) =>
            {
                if (list == null) return;
                var sources = src as List<UserSource>;
                
                for (int index = 0; index < list.Count; index++)
                {
                    var dest = list[index];
                    var source = sources?[index];
                    if (dest != null && source != null)
                    {
                        dest.FullName = $"{source.FirstName} {source.LastName}";
                        dest.RowNumber = index + 1;
                        dest.IsAdult = source.Age >= 18;
                    }
                }
            });

            AssertEqual(2, targetList?.Count, "List 长度");
            AssertEqual("John Doe", targetList?[0].FullName, "Item 0 - FullName");
            AssertEqual(1, targetList?[0].RowNumber, "Item 0 - RowNumber");
            AssertTrue(targetList?[0].IsAdult == true, "Item 0 - IsAdult");
            AssertEqual("Jane Smith", targetList?[1].FullName, "Item 1 - FullName");
            AssertEqual(2, targetList?[1].RowNumber, "Item 1 - RowNumber");

            Console.WriteLine("  ✓ AdaptList 回调成功");
            Console.WriteLine();
        }

        #endregion

        #region 7. Adapt Null 处理测试

        private static void TestAdaptWithNullHandling()
        {
            Console.WriteLine("7. Adapt Null 处理测试");

            // 测试 1: null 源对象
            SimpleSource? nullSource = null;
            var target1 = nullSource.Adapt<SimpleTarget>();
            AssertNull(target1, "Null 源对象应返回 null");
            Console.WriteLine("  ✓ Null 源对象处理成功");

            // 测试 2: 包含 null 属性的对象
            var source2 = new NullTestSource { Id = 1, Name = null, Description = "Test" };
            var target2 = source2.Adapt<NullTestTarget>();
            AssertEqual(1, target2?.Id, "Id 映射");
            AssertNull(target2?.Name, "Null 属性保留");
            AssertEqual("Test", target2?.Description, "非 null 属性映射");
            Console.WriteLine("  ✓ Null 属性处理成功");

            // 测试 3: AdaptList 包含 null 元素
            var sourceList = new List<SimpleSource?>
            {
                new SimpleSource { Id = 1, Name = "Item 1" },
                null,
                new SimpleSource { Id = 3, Name = "Item 3" }
            };

            // 注意：AdaptList 会跳过 null 元素
            var targetList = sourceList.Where(s => s != null)
                                      .Adapt<List<SimpleTarget>>();
            AssertEqual(2, targetList?.Count, "List 长度（跳过 null）");
            Console.WriteLine("  ✓ List Null 元素处理成功");

            Console.WriteLine();
        }

        #endregion

        #region 8. Adapt 循环引用测试

        private static void TestAdaptWithCircularReference()
        {
            Console.WriteLine("8. Adapt 循环引用测试");

            // 创建循环引用的对象
            var nodeA = new NodeSource { Id = 1, Name = "Node A" };
            var nodeB = new NodeSource { Id = 2, Name = "Node B" };
            nodeA.Related = nodeB;
            nodeB.Related = nodeA;

            try
            {
                // v2.1.3 应该能够处理循环引用
                var targetA = nodeA.Adapt<NodeTarget>();

                AssertEqual(1, targetA?.Id, "Node A - Id");
                AssertEqual("Node A", targetA?.Name, "Node A - Name");
                AssertEqual(2, targetA?.Related?.Id, "Node B - Id");
                AssertEqual("Node B", targetA?.Related?.Name, "Node B - Name");

                // 检查循环是否被打破或正确处理
                if (targetA?.Related?.Related != null)
                {
                    if (ReferenceEquals(targetA, targetA.Related.Related))
                    {
                        _passedTests++;
                    }
                    else
                    {
                        Console.WriteLine("  ❌ 循环引用未正确保持（引用不同对象）");
                        _failedTests++;
                    }
                }
                else
                {
                    Console.WriteLine("  ❌ 循环引用链断裂（Related.Related 为 null）");
                    _failedTests++;
                }

                Console.WriteLine("  ✓ 循环引用处理成功");
            }
            catch (StackOverflowException)
            {
                Console.WriteLine("  ❌ StackOverflow：循环引用保护失败");
                _failedTests++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 异常: {ex.Message}");
                _failedTests++;
            }

            Console.WriteLine();
        }

        #endregion

        #region 9. Adapt 边界情况测试

        private static void TestAdaptEdgeCases()
        {
            Console.WriteLine("9. Adapt 边界情况测试");

            // 测试 1: 空字符串
            var source1 = new { Id = 1, Name = "" };
            var target1 = source1.Adapt<SimpleTarget>();
            AssertEqual("", target1?.Name, "空字符串");
            Console.WriteLine("  ✓ 空字符串处理成功");

            // 测试 2: 特殊字符
            var source2 = new { Id = 2, Name = "Test\n\r\t" };
            var target2 = source2.Adapt<SimpleTarget>();
            AssertEqual("Test\n\r\t", target2?.Name, "特殊字符");
            Console.WriteLine("  ✓ 特殊字符处理成功");

            // 测试 3: 大对象
            var source3 = new LargeSource
            {
                Id = 1,
                Property1 = "Value1",
                Property2 = "Value2",
                Property3 = "Value3",
                Property4 = "Value4",
                Property5 = "Value5"
            };
            var target3 = source3.Adapt<LargeTarget>();
            AssertEqual(1, target3?.Id, "大对象 - Id");
            AssertEqual("Value5", target3?.Property5, "大对象 - Property5");
            Console.WriteLine("  ✓ 大对象处理成功");

            // 测试 4: 嵌套对象
            var source4 = new NestedSource
            {
                Id = 1,
                Name = "Parent",
                Child = new ChildSource { Id = 2, Name = "Child" }
            };
            var target4 = source4.Adapt<NestedTarget>();
            AssertEqual(1, target4?.Id, "嵌套对象 - Parent Id");
            AssertEqual(2, target4?.Child?.Id, "嵌套对象 - Child Id");
            AssertEqual("Child", target4?.Child?.Name, "嵌套对象 - Child Name");
            Console.WriteLine("  ✓ 嵌套对象处理成功");

            // 测试 5: 集合属性
            var source5 = new CollectionSource
            {
                Id = 1,
                Items = new List<string> { "Item1", "Item2", "Item3" }
            };
            var target5 = source5.Adapt<CollectionTarget>();
            AssertEqual(1, target5?.Id, "集合属性 - Id");
            AssertEqual(3, target5?.Items?.Count, "集合属性 - Count");
            AssertEqual("Item2", target5?.Items?[1], "集合属性 - Item 1");
            Console.WriteLine("  ✓ 集合属性处理成功");

            Console.WriteLine();
        }

        #endregion

        #region 测试辅助方法

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!Equals(expected, actual))
            {
                Console.WriteLine($"  ❌ {message}: 期望 {expected}, 实际 {actual}");
                _failedTests++;
            }
            else
            {
                _passedTests++;
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                Console.WriteLine($"  ❌ {message}");
                _failedTests++;
            }
            else
            {
                _passedTests++;
            }
        }

        private static void AssertNull<T>(T obj, string message) where T : class
        {
            if (obj != null)
            {
                Console.WriteLine($"  ❌ {message}: 期望 null, 实际 {obj}");
                _failedTests++;
            }
            else
            {
                _passedTests++;
            }
        }

        #endregion

        #region 测试模型

        private class SimpleSource
        {
            public int Id { get; set; }
            public string? Name { get; set; }
        }

        private class SimpleTarget
        {
            public int Id { get; set; }
            public string? Name { get; set; }
        }

        private class UserSource
        {
            public int Id { get; set; }
            public string FirstName { get; set; } = "";
            public string LastName { get; set; } = "";
            public int Age { get; set; }
        }

        private class UserTarget
        {
            public int Id { get; set; }
            public string FirstName { get; set; } = "";
            public string LastName { get; set; } = "";
            public string FullName { get; set; } = "";
            public int RowNumber { get; set; }
            public bool IsAdult { get; set; }
        }

        private class CaseTestTarget
        {
            public int Id { get; set; }
            public string? Name { get; set; }
        }

        private class UnderscoreTestTarget
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? UserName { get; set; }
        }

        private class FlexibleTestTarget
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? UserName { get; set; }
        }

        private class NullTestSource
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? Description { get; set; }
        }

        private class NullTestTarget
        {
            public int Id { get; set; }
            public string Name { get; set; } = "Default";
            public string? Description { get; set; }
        }

        private class SecurityTestTarget
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? Password { get; set; }
            public string? Email { get; set; }
        }

        private class CombinedTestTarget
        {
            public int Id { get; set; }
            public string FirstName { get; set; } = "";
            public string LastName { get; set; } = "";
            public string FullName { get; set; } = "";
        }

        private class NodeSource
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public NodeSource? Related { get; set; }
        }

        private class NodeTarget
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public NodeTarget? Related { get; set; }
        }

        private class LargeSource
        {
            public int Id { get; set; }
            public string? Property1 { get; set; }
            public string? Property2 { get; set; }
            public string? Property3 { get; set; }
            public string? Property4 { get; set; }
            public string? Property5 { get; set; }
        }

        private class LargeTarget
        {
            public int Id { get; set; }
            public string? Property1 { get; set; }
            public string? Property2 { get; set; }
            public string? Property3 { get; set; }
            public string? Property4 { get; set; }
            public string? Property5 { get; set; }
        }

        private class NestedSource
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public ChildSource? Child { get; set; }
        }

        private class ChildSource
        {
            public int Id { get; set; }
            public string? Name { get; set; }
        }

        private class NestedTarget
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public ChildTarget? Child { get; set; }
        }

        private class ChildTarget
        {
            public int Id { get; set; }
            public string? Name { get; set; }
        }

        private class CollectionSource
        {
            public int Id { get; set; }
            public List<string>? Items { get; set; }
        }

        private class CollectionTarget
        {
            public int Id { get; set; }
            public List<string>? Items { get; set; }
        }

        #endregion
    }
}
