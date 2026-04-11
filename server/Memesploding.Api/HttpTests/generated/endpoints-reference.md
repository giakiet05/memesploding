# API Endpoints Reference (Auto-generated from Code)

## Summary
- **Total Endpoints**: 31
- **Authenticated**: 29
- **Public**: 2

## Authentication (AuthController)
| Method | Endpoint | Auth | Request | Response |
|--------|----------|------|---------|----------|
| POST | `/api/v1/auth/guest` | ❌ | `RegisterGuestRequestDto` | `AuthResponseDto` |
| POST | `/api/v1/auth/google` | ❌ | `LoginGoogleRequestDto` | `AuthResponseDto` |
| POST | `/api/v1/auth/refresh` | ❌ | `RefreshTokenRequestDto` | `AuthResponseDto` |
| POST | `/api/v1/auth/logout` | ✅ | `RefreshTokenRequestDto` | `object` |

## Users (UserController)
| Method | Endpoint | Auth | Request | Response |
|--------|----------|------|---------|----------|
| GET | `/api/v1/users/me/profile` | ✅ | - | `MeDto` |
| PATCH | `/api/v1/users/me/profile` | ✅ | `UpdateUserRequestDto` | `MeDto` |
| GET | `/api/v1/users/me/stats` | ✅ | - | `UserStatsDto` |
| GET | `/api/v1/users/{userId}` | ✅ | - | `UserProfileDto` |
| GET | `/api/v1/users/{userId}/stats` | ✅ | - | `UserStatsDto` |
| GET | `/api/v1/users` | ✅ | Query params | `ListResponseData<UserProfileDto>` |
| GET | `/api/v1/users/leaderboard` | ❌ | Query params | `ListResponseData<UserProfileDto>` |

## Friendships (FriendsController)
| Method | Endpoint | Auth | Request | Response |
|--------|----------|------|---------|----------|
| GET | `/api/v1/me/friends` | ✅ | Query params | `ListResponseData<UserProfileDto>` |
| POST | `/api/v1/me/friends/invitations` | ✅ | `FriendshipRequestDto` | `UserProfileDto` |
| PATCH | `/api/v1/me/friends/invitations/{requesterId}` | ✅ | `ProcessFriendRequestDto` | `UserProfileDto` |
| DELETE | `/api/v1/me/friends/{friendId}` | ✅ | - | `object` |

## Card Sets (CardSetsController)
| Method | Endpoint | Auth | Request | Response |
|--------|----------|------|---------|----------|
| GET | `/api/v1/card-sets` | ✅ | Query params | `ListResponseData<CardSetDto>` |
| GET | `/api/v1/card-sets/{id}/cards` | ✅ | Query params | `ListResponseData<CardDto>` |

## Rooms (RoomsController)
| Method | Endpoint | Auth | Request | Response |
|--------|----------|------|---------|----------|
| POST | `/api/v1/rooms` | ✅ | `CreateRoomDto` | `RoomDetailDto` |
| GET | `/api/v1/rooms` | ✅ | Query params | `ListResponseData<RoomSummaryDto>` |
| GET | `/api/v1/rooms/{code}` | ✅ | - | `RoomDetailDto` |

## Notifications (NotificationsController)
| Method | Endpoint | Auth | Request | Response |
|--------|----------|------|---------|----------|
| GET | `/api/v1/me/notifications` | ✅ | Query params | `ListResponseData<NotificationDto>` |
| GET | `/api/v1/me/notifications/unread-count` | ✅ | - | `UnreadCountDto` |
| PATCH | `/api/v1/notifications/{id}` | ✅ | - | `NotificationDto` |
| DELETE | `/api/v1/notifications/{id}` | ✅ | - | `object` |
| PATCH | `/api/v1/notifications/mark-all-read` | ✅ | - | `object` |
| DELETE | `/api/v1/notifications/clear-all` | ✅ | - | `object` |

## Matches (MatchesController)
| Method | Endpoint | Auth | Request | Response |
|--------|----------|------|---------|----------|
| GET | `/api/v1/me/match-history` | ✅ | Query params | `ListResponseData<MatchSummaryDto>` |
| GET | `/api/v1/users/{id}/match-history` | ✅ | Query params | `ListResponseData<MatchSummaryDto>` |
| GET | `/api/v1/matches/{matchId}` | ✅ | - | `MatchDetailDto` |

## Matchmaking (MatchmakingController)
| Method | Endpoint | Auth | Request | Response |
|--------|----------|------|---------|----------|
| POST | `/api/v1/matchmaking/quick-play` | ✅ | - | `RoomDetailDto` |

## Response Format
All responses wrapped in:
```json
{
  "Message": "string",
  "Data": { ... }
}
```

## Field Naming Convention
**All fields use PascalCase in responses:**
- `AccessToken` (not `access_token`)
- `RefreshToken` (not `refresh_token`)
- `IsNewUser` (not `is_new_user`)
- `AvatarUrl` (not `avatar_url`)
- `CardSetIds` (not `card_set_ids`)
- etc.
