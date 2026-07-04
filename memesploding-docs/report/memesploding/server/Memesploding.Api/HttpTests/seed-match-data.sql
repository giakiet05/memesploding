--
-- Seed Match Data for Integration Testing
-- Creates realistic match between Alice and Bob
--
-- Prerequisites:
-- 1. Alice and Bob users must exist (created by integration tests)
-- 2. Get user IDs from: SELECT id, username FROM users WHERE username IN ('alice_auto', 'bob_auto');
-- 3. Replace {ALICE_ID} and {BOB_ID} with actual UUIDs
--

-- Step 1: Get user IDs (run this first and copy the IDs)
SELECT id, username FROM users WHERE username IN ('alice_auto', 'alice_integration', 'bob_auto', 'bob_integration');

--
-- Then replace the placeholders below with actual UUIDs
--

-- Generate a match ID (or use a specific UUID)
-- Example: gen_random_uuid() or '123e4567-e89b-12d3-a456-426614174000'

-- Insert a completed match
INSERT INTO matches (id, room_code, status, winner_id, started_at, ended_at, duration_seconds, created_at, updated_at)
VALUES (
  gen_random_uuid(),  -- Match ID
  'TEST123',  -- Room code
  'completed',  -- Status
  '{ALICE_ID}',  -- Winner (replace with actual Alice UUID)
  NOW() - INTERVAL '2 hours',  -- Started 2 hours ago
  NOW() - INTERVAL '1 hour 30 minutes',  -- Ended 1.5 hours ago
  1800,  -- 30 minutes duration
  NOW() - INTERVAL '2 hours',
  NOW() - INTERVAL '1 hour 30 minutes'
)
RETURNING id;  -- Copy this ID for next step

-- Store the match ID
-- \gset match_id  -- For psql interactive mode

-- Insert match participants
-- Replace {MATCH_ID} with the ID from previous step
INSERT INTO match_participants (id, match_id, user_id, placement, eliminations, cards_played, created_at, updated_at)
VALUES 
  (
    gen_random_uuid(),
    '{MATCH_ID}',  -- Replace with match ID
    '{ALICE_ID}',  -- Replace with Alice UUID
    1,  -- Winner (1st place)
    2,  -- Eliminated 2 players
    18,  -- Played 18 cards
    NOW() - INTERVAL '1 hour 30 minutes',
    NOW() - INTERVAL '1 hour 30 minutes'
  ),
  (
    gen_random_uuid(),
    '{MATCH_ID}',  -- Replace with match ID
    '{BOB_ID}',  -- Replace with Bob UUID
    2,  -- Runner-up (2nd place)
    1,  -- Eliminated 1 player
    15,  -- Played 15 cards
    NOW() - INTERVAL '1 hour 30 minutes',
    NOW() - INTERVAL '1 hour 30 minutes'
  );

-- Optional: Insert match timeline events (simplified version)
-- This is a minimal example - real matches have many more events

-- Event 1: Match started
INSERT INTO match_timeline_events (id, match_id, event_type, user_id, event_data, created_at)
VALUES (
  gen_random_uuid(),
  '{MATCH_ID}',
  'match_started',
  NULL,
  '{"participants": ["alice_auto", "bob_auto"]}'::jsonb,
  NOW() - INTERVAL '2 hours'
);

-- Event 2: Turn started
INSERT INTO match_timeline_events (id, match_id, event_type, user_id, event_data, created_at)
VALUES (
  gen_random_uuid(),
  '{MATCH_ID}',
  'turn_started',
  '{ALICE_ID}',
  '{"turn": 1, "player": "alice_auto"}'::jsonb,
  NOW() - INTERVAL '2 hours' + INTERVAL '5 seconds'
);

-- Event 3: Card played
INSERT INTO match_timeline_events (id, match_id, event_type, user_id, event_data, created_at)
VALUES (
  gen_random_uuid(),
  '{MATCH_ID}',
  'card_played',
  '{ALICE_ID}',
  '{"card": "attack", "target": null}'::jsonb,
  NOW() - INTERVAL '2 hours' + INTERVAL '10 seconds'
);

-- Event 4: Player eliminated
INSERT INTO match_timeline_events (id, match_id, event_type, user_id, event_data, created_at)
VALUES (
  gen_random_uuid(),
  '{MATCH_ID}',
  'player_eliminated',
  '{BOB_ID}',
  '{"eliminated_by": "alice_auto", "card": "exploding_kitten"}'::jsonb,
  NOW() - INTERVAL '1 hour 31 minutes'
);

-- Event 5: Match ended
INSERT INTO match_timeline_events (id, match_id, event_type, user_id, event_data, created_at)
VALUES (
  gen_random_uuid(),
  '{MATCH_ID}',
  'match_ended',
  NULL,
  '{"winner": "alice_auto", "duration_seconds": 1800}'::jsonb,
  NOW() - INTERVAL '1 hour 30 minutes'
);

-- Update user stats (optional - may be auto-calculated)
UPDATE users 
SET 
  games_played = games_played + 1,
  wins = wins + 1,
  total_playtime_seconds = total_playtime_seconds + 1800,
  updated_at = NOW()
WHERE id = '{ALICE_ID}';

UPDATE users 
SET 
  games_played = games_played + 1,
  losses = losses + 1,
  total_playtime_seconds = total_playtime_seconds + 1800,
  updated_at = NOW()
WHERE id = '{BOB_ID}';

-- Verify data inserted
SELECT 
  m.id,
  m.room_code,
  m.status,
  m.duration_seconds,
  (SELECT username FROM users WHERE id = m.winner_id) as winner,
  COUNT(mp.id) as participant_count
FROM matches m
LEFT JOIN match_participants mp ON mp.match_id = m.id
WHERE m.room_code = 'TEST123'
GROUP BY m.id;

-- Verify participants
SELECT 
  u.username,
  mp.placement,
  mp.eliminations,
  mp.cards_played
FROM match_participants mp
JOIN users u ON u.id = mp.user_id
JOIN matches m ON m.id = mp.match_id
WHERE m.room_code = 'TEST123'
ORDER BY mp.placement;

-- Verify timeline
SELECT 
  event_type,
  (SELECT username FROM users WHERE id = mte.user_id) as player,
  event_data,
  created_at
FROM match_timeline_events mte
JOIN matches m ON m.id = mte.match_id
WHERE m.room_code = 'TEST123'
ORDER BY created_at;

--
-- ALTERNATIVE: Automated script with psql variables
--

\set alice_username 'alice_auto'
\set bob_username 'bob_auto'

-- Get IDs into variables
SELECT id FROM users WHERE username = :'alice_username' \gset alice_
SELECT id FROM users WHERE username = :'bob_username' \gset bob_

-- Create match with variables
WITH new_match AS (
  INSERT INTO matches (id, room_code, status, winner_id, started_at, ended_at, duration_seconds, created_at, updated_at)
  VALUES (
    gen_random_uuid(),
    'AUTO_TEST',
    'completed',
    :'alice_id',
    NOW() - INTERVAL '2 hours',
    NOW() - INTERVAL '90 minutes',
    1800,
    NOW() - INTERVAL '2 hours',
    NOW() - INTERVAL '90 minutes'
  )
  RETURNING id
)
INSERT INTO match_participants (id, match_id, user_id, placement, eliminations, cards_played, created_at, updated_at)
SELECT 
  gen_random_uuid(),
  (SELECT id FROM new_match),
  :'alice_id',
  1,
  2,
  18,
  NOW() - INTERVAL '90 minutes',
  NOW() - INTERVAL '90 minutes'
UNION ALL
SELECT 
  gen_random_uuid(),
  (SELECT id FROM new_match),
  :'bob_id',
  2,
  1,
  15,
  NOW() - INTERVAL '90 minutes',
  NOW() - INTERVAL '90 minutes';

-- Success message
\echo 'Match data seeded successfully!'
\echo 'Run integration tests Phase 8 to view match history'
