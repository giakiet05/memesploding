# 1. ECR repository for the API Server
resource "aws_ecr_repository" "api_server_repo" {
    name = "memesploding-api-server"
    image_tag_mutability = "MUTABLE" # Allow image tags to be overwritten
    image_scanning_configuration {
      scan_on_push = true # Automatically scan for vulnerabilities when an image is pushed
    }
    tags = {
        Name = "Memesploding-API-Server-Repo"
    }
}


# 2. ECR repository for the Game Server
resource "aws_ecr_repository" "game_server_repo" {
    name = "memesploding-game-server"
    image_tag_mutability = "MUTABLE" # Allow image tags to be overwritten
    image_scanning_configuration {
      scan_on_push = true # Automatically scan for vulnerabilities when an image is pushed
    }
    tags = {
        Name = "Memesploding-Game-Server-Repo"
    }
}

