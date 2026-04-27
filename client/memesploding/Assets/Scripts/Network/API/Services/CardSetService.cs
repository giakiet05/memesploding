using System.Collections.Generic;
using System.Threading.Tasks;
using Network.API.Models;

namespace Network.API.Services
{
    public class CardSetService
    {
        private static CardSetService _instance;
        public static CardSetService Instance => _instance ??= new CardSetService();

        private readonly string _baseUrl = $"{Config.Api.baseUrl}/api/v1/card-sets";

        private CardSetService() { }

        public Task<ApiResponse<ListResponseData<CardSetDto>>> GetCardSetsAsync(PaginationQueryDto pagination, string accessToken)
        {
            var url = ApiQueryBuilder.Build(_baseUrl, BuildPagination(pagination));
            return ApiClient.Instance.GetAsync<ListResponseData<CardSetDto>>(url, accessToken);
        }

        public Task<ApiResponse<ListResponseData<CardDto>>> GetCardSetCardsAsync(string cardSetId, PaginationQueryDto pagination, string accessToken)
        {
            var url = ApiQueryBuilder.Build($"{_baseUrl}/{cardSetId}/cards", BuildPagination(pagination));
            return ApiClient.Instance.GetAsync<ListResponseData<CardDto>>(url, accessToken);
        }

        private static Dictionary<string, string> BuildPagination(PaginationQueryDto pagination)
        {
            return new Dictionary<string, string>
            {
                ["Page"] = (pagination?.Page ?? 1).ToString(),
                ["PageSize"] = (pagination?.PageSize ?? 20).ToString()
            };
        }
    }
}
