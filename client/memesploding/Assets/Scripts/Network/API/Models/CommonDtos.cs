using System;
using System.Collections.Generic;

namespace Network.API.Models
{
    [Serializable]
    public class PaginationQueryDto
    {
        public int Page = 1;
        public int PageSize = 20;
    }

    [Serializable]
    public class PaginationMeta
    {
        public int Page;
        public int PageSize;
        public int TotalCount;
        public bool HasMore;
    }

    [Serializable]
    public class ListResponseData<T>
    {
        public List<T> Items;
        public PaginationMeta Pagination;
    }
}
