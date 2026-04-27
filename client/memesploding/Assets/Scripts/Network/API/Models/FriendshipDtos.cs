using System;

namespace Network.API.Models
{
    [Serializable]
    public class FriendshipQueryDto
    {
        public string Status;
        public PaginationQueryDto Pagination = new PaginationQueryDto();
    }

    [Serializable]
    public class FriendshipRequestDto
    {
        public string UserId;
    }

    [Serializable]
    public class ProcessFriendRequestDto
    {
        public bool Accept;
    }
}
