using Network.API.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Managers
{
    public class RoomManager : MonoBehaviour
    {
        public static RoomManager Instance;

        public static RoomManager EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            var gameObject = new GameObject(nameof(RoomManager));
            return gameObject.AddComponent<RoomManager>();
        }

        public RoomDetailDto CurrentRoom { get; private set; }
        public string RequestedRoomName { get; private set; }
        public bool HasRoom => CurrentRoom != null && !string.IsNullOrWhiteSpace(CurrentRoom.Code);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetCurrentRoom(RoomDetailDto roomDetail, string requestedRoomName = null)
        {
            CurrentRoom = roomDetail;
            RequestedRoomName = requestedRoomName ?? string.Empty;
        }

        public void SetStatus(string status)
        {
            EnsureRoomInitialized();
            CurrentRoom.Status = status ?? string.Empty;
        }

        public void SetGameplayConnection(string wsUrl, string wsAccessToken)
        {
            EnsureRoomInitialized();
            CurrentRoom.Connection ??= new RoomConnectionDto();
            CurrentRoom.Connection.WsUrl = wsUrl ?? string.Empty;
            CurrentRoom.Connection.WsAccessToken = wsAccessToken ?? string.Empty;
        }

        public void UpsertParticipant(string userId, string nickname, string avatarUrl, string role, bool isReady)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return;

            EnsureParticipants();

            var existing = CurrentRoom.CurrentParticipants.FirstOrDefault(participant =>
                IdEquals(participant.UserId, userId));

            if (existing != null)
            {
                existing.Nickname = nickname ?? existing.Nickname ?? string.Empty;
                existing.AvatarUrl = avatarUrl ?? existing.AvatarUrl ?? string.Empty;
                existing.Role = role ?? existing.Role ?? string.Empty;
                existing.IsReady = isReady;
                return;
            }

            CurrentRoom.CurrentParticipants.Add(new RoomParticipantDto
            {
                UserId = userId,
                Nickname = nickname ?? string.Empty,
                AvatarUrl = avatarUrl ?? string.Empty,
                Role = role ?? string.Empty,
                IsReady = isReady
            });
        }

        public void RemoveParticipant(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId) || CurrentRoom?.CurrentParticipants == null)
                return;

            CurrentRoom.CurrentParticipants.RemoveAll(participant => IdEquals(participant.UserId, userId));
        }

        public void UpdateReadyStatus(string userId, bool isReady)
        {
            if (string.IsNullOrWhiteSpace(userId) || CurrentRoom?.CurrentParticipants == null)
                return;

            var participant = CurrentRoom.CurrentParticipants.FirstOrDefault(item => IdEquals(item.UserId, userId));
            if (participant != null)
                participant.IsReady = isReady;
        }

        public void UpdateHost(string newHostUserId)
        {
            if (string.IsNullOrWhiteSpace(newHostUserId))
                return;

            EnsureRoomInitialized();
            CurrentRoom.HostId = newHostUserId;

            if (CurrentRoom.CurrentParticipants == null)
                return;

            foreach (var participant in CurrentRoom.CurrentParticipants)
                participant.Role = IdEquals(participant.UserId, newHostUserId) ? "Host" : "Player";
        }

        public List<RoomParticipantDto> GetOrderedParticipants()
        {
            if (CurrentRoom?.CurrentParticipants == null || CurrentRoom.CurrentParticipants.Count == 0)
                return new List<RoomParticipantDto>();

            return CurrentRoom.CurrentParticipants
                .Select((participant, index) => new { participant, index })
                .OrderBy(item => IdEquals(item.participant.UserId, CurrentRoom.HostId) ? 0 : 1)
                .ThenBy(item => item.index)
                .Select(item => item.participant)
                .ToList();
        }

        public bool IsLocalPlayerHost()
        {
            var localUserId = GameManager.EnsureInstance().Player?.ID;
            return IdEquals(CurrentRoom?.HostId, localUserId);
        }

        public bool IsLocalPlayerReady()
        {
            var localUserId = GameManager.EnsureInstance().Player?.ID;
            if (string.IsNullOrWhiteSpace(localUserId) || CurrentRoom?.CurrentParticipants == null)
                return false;

            var participant = CurrentRoom.CurrentParticipants.FirstOrDefault(item => IdEquals(item.UserId, localUserId));
            return participant != null && participant.IsReady;
        }

        public bool CanLocalPlayerStartMatch()
        {
            if (!IsLocalPlayerHost())
                return false;

            var participants = GetOrderedParticipants();
            if (participants.Count < 2)
                return false;

            return participants
                .Where(participant => !IdEquals(participant.UserId, CurrentRoom.HostId))
                .All(participant => participant.IsReady);
        }

        public void ClearCurrentRoom()
        {
            CurrentRoom = null;
            RequestedRoomName = string.Empty;
        }

        public string GetDisplayRoomName()
        {
            if (!string.IsNullOrWhiteSpace(RequestedRoomName))
                return RequestedRoomName;

            if (!string.IsNullOrWhiteSpace(CurrentRoom?.Code))
                return $"Room {CurrentRoom.Code}";

            return "Waiting Room";
        }

        public int GetParticipantCount()
        {
            return CurrentRoom?.CurrentParticipants?.Count ?? 0;
        }

        public int GetMaxPlayers()
        {
            return CurrentRoom?.Settings?.MaxPlayers ?? 0;
        }

        public string GetRoomCode()
        {
            return CurrentRoom?.Code ?? string.Empty;
        }

        public string GetWsUrl()
        {
            return CurrentRoom?.Connection?.WsUrl ?? string.Empty;
        }

        public string GetWsAccessToken()
        {
            return CurrentRoom?.Connection?.WsAccessToken ?? string.Empty;
        }

        private void EnsureRoomInitialized()
        {
            CurrentRoom ??= new RoomDetailDto
            {
                Code = string.Empty,
                HostId = string.Empty,
                Status = string.Empty,
                Settings = new RoomSettingsDto(),
                CurrentParticipants = new List<RoomParticipantDto>(),
                Connection = new RoomConnectionDto()
            };
        }

        private void EnsureParticipants()
        {
            EnsureRoomInitialized();
            CurrentRoom.CurrentParticipants ??= new List<RoomParticipantDto>();
        }

        private static bool IdEquals(string left, string right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }
}
