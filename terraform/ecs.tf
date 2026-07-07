# 1. Create ECS Cluster
resource "aws_ecs_cluster" "main" {
  name = "memesploding-ecs-cluster"
  tags = {
    Name = "Memesploding-ECS-Cluster"
  }
}
# 2. IAM Role for ECS Task Execution
resource "aws_iam_role" "ecs_task_execution_role" {
  name = "memesploding-ecs-task-execution-role"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "ecs-tasks.amazonaws.com"
      }
    }]
  })
}

# 3. Attach the AmazonECSTaskExecutionRolePolicy to the IAM Role
resource "aws_iam_role_policy_attachment" "ecs_task_execution_role_policy" {
  role       = aws_iam_role.ecs_task_execution_role.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"

}

# 4. Task Definition for the API Server
resource "aws_ecs_task_definition" "api" {
  family                   = "memesploding-api-task"
  network_mode             = "awsvpc"                                 # Use the awsvpc network mode for Fargate tasks
  requires_compatibilities = ["FARGATE"]                              # Specify that this task runs on Fargate
  cpu                      = "512"                                    # 0.5 vCPU
  memory                   = "1024"                                   # 1 GB RAM
  execution_role_arn       = aws_iam_role.ecs_task_execution_role.arn # Use the IAM role for task execution

  # Container Definitions
  container_definitions = jsonencode([
    {
      name      = "api-container"
      image     = "${aws_ecr_repository.api_server_repo.repository_url}:latest" # Use the latest image from ECR
      essential = true
      logConfiguration = {
        logDriver = "awslogs"

        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.api.name
          "awslogs-region"        = "ap-southeast-1"
          "awslogs-stream-prefix" = "ecs"
        }
      }

      # Open port 5217 for the API Server for communication with the ALB
      portMappings = [
        {
          containerPort = 5217
          hostPort      = 5217
          protocol      = "tcp"
        }
      ]

      environment = [
        {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = "Production"
        },
        {
          name  = "ASPNETCORE_URLS"
          value = "http://+:5217"
        },
        {
          name  = "ConnectionStrings__DefaultConnection"
          value = "Host=${aws_db_instance.postgres.address};Port=${aws_db_instance.postgres.port};Database=${aws_db_instance.postgres.db_name};Username=${aws_db_instance.postgres.username};Password=${var.db_password}"
        },
        {
          name  = "ConnectionStrings__RedisConnection"
          value = "${aws_elasticache_cluster.redis.cache_nodes[0].address}:6379,abortConnect=false"
        },
        {
          name  = "Jwt__Key"
          value = var.jwt_key
        },
        {
          name  = "Google__ClientId"
          value = var.google_client_id
        },
        {
          name  = "Google__ClientSecret"
          value = var.google_client_secret
        },
        {
          name  = "GameTicket__Key"
          value = var.game_ticket_key
        },
        {
          name  = "Realtime__GameWsUrl"
          value = "wss://${var.custom_domain}/ws"
        }
      ]
    }
    ]
  )
}

# 5. Build the ECS Service for the API Server
resource "aws_ecs_service" "api" {
  name            = "memesploding-api-service"
  cluster         = aws_ecs_cluster.main.id         # Reference the ECS cluster
  task_definition = aws_ecs_task_definition.api.arn # Reference the API Server task definition
  desired_count   = 1                               # Number of tasks to run
  launch_type     = "FARGATE"                       # Use Fargate launch type

  network_configuration {
    subnets = [
      aws_subnet.public_subnet_1.id,
      aws_subnet.public_subnet_2.id
    ]
    security_groups  = [aws_security_group.ecs_sg.id]
    assign_public_ip = true # BẮT BUỘC TRÊN PUBLIC SUBNET NẾU KHÔNG CÓ NAT GATEWAY
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.api.arn # Reference the API Server target group
    container_name   = "api-container"             # Name of the container in the task definition
    container_port   = 5217                        # Port on which the container listens
  }

  depends_on = [aws_lb_listener.http]

  lifecycle {
    ignore_changes = [
      task_definition
    ]
  }
}

# 6. Task Definition for the Game Server
resource "aws_ecs_task_definition" "game" {
  family                   = "memesploding-game-task"
  network_mode             = "awsvpc"                                 # Use the awsvpc network mode for Fargate tasks
  requires_compatibilities = ["FARGATE"]                              # Specify that this task runs on Fargate
  cpu                      = "512"                                    # 0.5 vCPU
  memory                   = "1024"                                   # 1 GB RAM
  execution_role_arn       = aws_iam_role.ecs_task_execution_role.arn # Use the IAM role for task execution 

  container_definitions = jsonencode([
    {
      name      = "game-container"
      image     = "${aws_ecr_repository.game_server_repo.repository_url}:latest" # Use the latest image from ECR
      essential = true

      logConfiguration = {
        logDriver = "awslogs"

        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.game.name
          "awslogs-region"        = "ap-southeast-1"
          "awslogs-stream-prefix" = "ecs"
        }
      }
      # Open port 5204 for the Game Server for communication with the ALB
      portMappings = [
        {
          containerPort = 5204
          hostPort      = 5204
          protocol      = "tcp"
        }
      ]

      environment = [
        {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = "Production"
        },
        {
          name  = "ASPNETCORE_URLS"
          value = "http://+:5204"
        },
        {
          name  = "ConnectionStrings__RedisConnection"
          value = "${aws_elasticache_cluster.redis.cache_nodes[0].address}:6379,abortConnect=false"
        },
        {
          name  = "Jwt__Key"
          value = var.jwt_key
        },
        {
          name  = "GameTicket__Key"
          value = var.game_ticket_key
        },
        {
          name  = "GameTicket__Issuer"
          value = "MemesplodingApi"
        },
        {
          name  = "GameTicket__Audience"
          value = "MemesplodingGameServer"
        }
      ]
    }
    ]
  )
}

# 7. Build the ECS Service for the Game Server
resource "aws_ecs_service" "game" {
  name            = "memesploding-game-service"
  cluster         = aws_ecs_cluster.main.id          # Reference the ECS cluster
  task_definition = aws_ecs_task_definition.game.arn # Reference the Game Server task definition
  desired_count   = 1                                # Number of tasks to run
  launch_type     = "FARGATE"                        # Use Fargate launch type

  network_configuration {
    subnets = [
      aws_subnet.public_subnet_1.id,
      aws_subnet.public_subnet_2.id
    ]
    security_groups  = [aws_security_group.ecs_sg.id]
    assign_public_ip = true # BẮT BUỘC TRÊN PUBLIC SUBNET NẾU KHÔNG CÓ NAT GATEWAY
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.game.arn # Reference the Game Server target group
    container_name   = "game-container"             # Name of the container in the task definition
    container_port   = 5204                         # Port on which the container listens
  }

  depends_on = [aws_lb_listener.http]

  lifecycle {
    ignore_changes = [
      task_definition
    ]
  }
}
