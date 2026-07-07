resource "aws_cloudwatch_log_group" "api" {
  name              = "/ecs/memesploding-api"
  retention_in_days = 7

  tags = {
    Name = "Memesploding-API-Logs"
  }
}

resource "aws_cloudwatch_log_group" "game" {
  name = "/ecs/memesploding-game"

  retention_in_days = 7
  tags = {
    Name = "Memesploding-Game-Logs"
  }
}
