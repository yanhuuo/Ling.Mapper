using System;
using System.Collections.Generic;
using Ling.Mapper.Provider;

namespace TestConsole.Test
{
    public static class ProductOnShelvesSelectTest
    {
        public static void Run()
        {
            Console.WriteLine("【测试】ProductOnShelves 批量选择映射");

            var mapper = MapperProvider.Current;

            var src = new
            {
                Select = new
                {
                    isCardLimit = 0,
                    orgBranchStrId = "e41be2fe-33dc-4784-93a4-d927c1c00814",
                    orgId = "e41be2fe-33dc-4784-93a4-d927c1c00814",
                    orgIdList = new string[] { "e41be2fe-33dc-4784-93a4-d927c1c00814", "0" },
                    page = 1,
                    productType = 3,
                    size = 20
                },
                SelectAll = new
                {
                    ids = new string[] { "4538774671138758656" },
                    isSelectAll = 0
                }
            };

            var dest = mapper.Map<ProductOnShelvesBatchSelectReq>(src);

            if (dest == null) throw new Exception("映射结果为 null");

            if (dest.Select == null) throw new Exception("Select 未映射");

            if (dest.Select.OrgIdList == null) throw new Exception("OrgIdList 为 null");

            if (dest.Select.OrgIdList.Count != 2) throw new Exception($"OrgIdList 元素数量不正确，期望 2，实际 {dest.Select.OrgIdList.Count}");

            if (dest.Select.OrgIdList[0] != "e41be2fe-33dc-4784-93a4-d927c1c00814")
                throw new Exception("OrgIdList[0] 值不匹配");

            if (dest.SelectAll == null) throw new Exception("SelectAll 未映射");

            if (dest.SelectAll.Ids == null) throw new Exception("SelectAll.Ids 为 null");

            if (dest.SelectAll.Ids.Count != 1) throw new Exception("SelectAll.Ids 数量不正确");

            if (dest.SelectAll.Ids[0] != "4538774671138758656") throw new Exception("SelectAll.Ids[0] 值不匹配");

            Console.WriteLine("✓ ProductOnShelves 映射测试通过\n");
        }
    }

    // 目标 DTO，仅用于测试
    public class ProductOnShelvesBatchSelectReq
    {
        public ProductOnShelvesSelectConditionReq? Select { get; set; }
        public ProductOnShelvesSelectAllReq SelectAll { get; set; } = new ProductOnShelvesSelectAllReq();
    }

    public class ProductOnShelvesSelectConditionReq
    {
        public int? IsCardLimit { get; set; }
        public string? OrgBranchStrId { get; set; }
        public string? OrgId { get; set; }
        public List<string>? OrgIdList { get; set; }
        public int Page { get; set; }
        public int ProductType { get; set; }
        public int Size { get; set; }
    }

    public class ProductOnShelvesSelectAllReq
    {
        public int? IsSelectAll { get; set; }
        public List<string>? NotIds { get; set; }
        public List<string>? Ids { get; set; }
    }
}
