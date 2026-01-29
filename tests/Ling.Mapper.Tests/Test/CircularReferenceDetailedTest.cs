using System;
using Ling.Mapper.Extensions;

namespace TestConsole.Test
{
    /// <summary>
    /// 循环引用详细测试
    /// </summary>
    public static class CircularReferenceDetailedTest
    {
        private static int _passedTests = 0;
        private static int _failedTests = 0;

        public static void Run()
        {
            Console.WriteLine("=== 循环引用详细测试 ===\n");

            _passedTests = 0;
            _failedTests = 0;

            Test1_SimpleCircularReference();
            Test2_SelfReference();
            Test3_DeepCircularReference();
            Test4_MultipleCircularReferences();

            Console.WriteLine($"\n📊 测试统计: ✅ {_passedTests} 通过, ❌ {_failedTests} 失败");
            if (_failedTests == 0)
            {
                Console.WriteLine("✅ 所有循环引用测试通过\n");
            }
            else
            {
                Console.WriteLine($"⚠️  {_failedTests} 个测试失败\n");
            }
        }

        private static void Test1_SimpleCircularReference()
        {
            Console.WriteLine("【测试1】简单循环引用 (A -> B -> A)");

            var nodeA = new Node { Id = 1, Name = "Node A" };
            var nodeB = new Node { Id = 2, Name = "Node B" };
            nodeA.Related = nodeB;
            nodeB.Related = nodeA;

            try
            {
                var targetA = nodeA.Adapt<NodeDto>();

                bool passed = true;
                if (targetA == null)
                {
                    Console.WriteLine("  ❌ 映射结果为 null");
                    _failedTests++;
                    passed = false;
                }
                else
                {
                    if (targetA.Id != 1 || targetA.Name != "Node A")
                    {
                        Console.WriteLine("  ❌ Node A 属性映射错误");
                        _failedTests++;
                        passed = false;
                    }

                    if (targetA.Related == null)
                    {
                        Console.WriteLine("  ❌ Related 属性为 null");
                        _failedTests++;
                        passed = false;
                    }
                    else
                    {
                        if (targetA.Related.Id != 2 || targetA.Related.Name != "Node B")
                        {
                            Console.WriteLine("  ❌ Node B 属性映射错误");
                            _failedTests++;
                            passed = false;
                        }

                        if (targetA.Related.Related == null)
                        {
                            Console.WriteLine("  ❌ 循环引用未保持：Related.Related 为 null");
                            _failedTests++;
                            passed = false;
                        }
                        else if (!ReferenceEquals(targetA, targetA.Related.Related))
                        {
                            Console.WriteLine("  ❌ 循环引用未正确保持：Related.Related 不是同一个对象");
                            _failedTests++;
                            passed = false;
                        }
                    }
                }

                if (passed)
                {
                    Console.WriteLine("  ✓ 简单循环引用正确处理");
                    _passedTests++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 异常: {ex.Message}");
                _failedTests++;
            }

            Console.WriteLine();
        }

        private static void Test2_SelfReference()
        {
            Console.WriteLine("【测试2】自我引用 (A -> A)");

            var node = new Node { Id = 1, Name = "Self Node" };
            node.Related = node;

            try
            {
                var target = node.Adapt<NodeDto>();

                bool passed = true;
                if (target == null)
                {
                    Console.WriteLine("  ❌ 映射结果为 null");
                    _failedTests++;
                    passed = false;
                }
                else if (target.Related == null)
                {
                    Console.WriteLine("  ❌ 自我引用未保持：Related 为 null");
                    _failedTests++;
                    passed = false;
                }
                else if (!ReferenceEquals(target, target.Related))
                {
                    Console.WriteLine("  ❌ 自我引用未正确保持：Related 不是同一个对象");
                    _failedTests++;
                    passed = false;
                }

                if (passed)
                {
                    Console.WriteLine("  ✓ 自我引用正确处理");
                    _passedTests++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 异常: {ex.Message}");
                _failedTests++;
            }

            Console.WriteLine();
        }

        private static void Test3_DeepCircularReference()
        {
            Console.WriteLine("【测试3】深度循环引用 (A -> B -> C -> A)");

            var nodeA = new Node { Id = 1, Name = "Node A" };
            var nodeB = new Node { Id = 2, Name = "Node B" };
            var nodeC = new Node { Id = 3, Name = "Node C" };
            nodeA.Related = nodeB;
            nodeB.Related = nodeC;
            nodeC.Related = nodeA;

            try
            {
                var targetA = nodeA.Adapt<NodeDto>();

                bool passed = true;
                if (targetA == null || targetA.Related == null || targetA.Related.Related == null)
                {
                    Console.WriteLine("  ❌ 映射链断裂");
                    _failedTests++;
                    passed = false;
                }
                else
                {
                    var targetB = targetA.Related;
                    var targetC = targetB.Related;
                    var backToA = targetC.Related;

                    if (backToA == null)
                    {
                        Console.WriteLine("  ❌ 循环引用未保持：最后一环为 null");
                        _failedTests++;
                        passed = false;
                    }
                    else if (!ReferenceEquals(targetA, backToA))
                    {
                        Console.WriteLine("  ❌ 深度循环引用未正确保持");
                        _failedTests++;
                        passed = false;
                    }
                }

                if (passed)
                {
                    Console.WriteLine("  ✓ 深度循环引用正确处理");
                    _passedTests++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 异常: {ex.Message}");
                _failedTests++;
            }

            Console.WriteLine();
        }

        private static void Test4_MultipleCircularReferences()
        {
            Console.WriteLine("【测试4】多个循环引用");

            var nodeA = new NodeWithMultipleRefs { Id = 1, Name = "Node A" };
            var nodeB = new NodeWithMultipleRefs { Id = 2, Name = "Node B" };
            var nodeC = new NodeWithMultipleRefs { Id = 3, Name = "Node C" };
            
            nodeA.Ref1 = nodeB;
            nodeA.Ref2 = nodeC;
            nodeB.Ref1 = nodeA;
            nodeC.Ref1 = nodeA;

            try
            {
                var targetA = nodeA.Adapt<NodeWithMultipleRefsDto>();

                bool passed = true;
                if (targetA == null || targetA.Ref1 == null || targetA.Ref2 == null)
                {
                    Console.WriteLine("  ❌ 基本映射失败");
                    _failedTests++;
                    passed = false;
                }
                else
                {
                    if (targetA.Ref1.Ref1 == null)
                    {
                        Console.WriteLine("  ❌ 第一个循环引用未保持");
                        _failedTests++;
                        passed = false;
                    }
                    else if (!ReferenceEquals(targetA, targetA.Ref1.Ref1))
                    {
                        Console.WriteLine("  ❌ 第一个循环引用未正确保持");
                        _failedTests++;
                        passed = false;
                    }

                    if (targetA.Ref2.Ref1 == null)
                    {
                        Console.WriteLine("  ❌ 第二个循环引用未保持");
                        _failedTests++;
                        passed = false;
                    }
                    else if (!ReferenceEquals(targetA, targetA.Ref2.Ref1))
                    {
                        Console.WriteLine("  ❌ 第二个循环引用未正确保持");
                        _failedTests++;
                        passed = false;
                    }
                }

                if (passed)
                {
                    Console.WriteLine("  ✓ 多个循环引用正确处理");
                    _passedTests++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 异常: {ex.Message}");
                _failedTests++;
            }

            Console.WriteLine();
        }

        // 测试模型
        public class Node
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public Node? Related { get; set; }
        }

        public class NodeDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public NodeDto? Related { get; set; }
        }

        public class NodeWithMultipleRefs
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public NodeWithMultipleRefs? Ref1 { get; set; }
            public NodeWithMultipleRefs? Ref2 { get; set; }
        }

        public class NodeWithMultipleRefsDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public NodeWithMultipleRefsDto? Ref1 { get; set; }
            public NodeWithMultipleRefsDto? Ref2 { get; set; }
        }
    }
}
