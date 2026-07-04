# 1. Create the ALB
resource "aws_lb" "main" {
  name = "memesploding-alb"
  internal = false # The ALB is internet-facing
  load_balancer_type = "application"
  security_groups = [aws_security_group.alb_sg.id] # Attach the ALB security group
  subnets = [
    aws_subnet.public_subnet_1.id,
    aws_subnet.public_subnet_2.id
  ]
  tags = {
    Name = "Memesploding-ALB"
  }
}

# 2. Create a Target Group for the API Server
resource "aws_lb_target_group" "api" {
    name = "memesploding-api-tg"
    port = 5217
    protocol = "HTTP"
    vpc_id = aws_vpc.main.id
    target_type = "ip" # Use IP addresses for ECS Fargate tasks
    health_check {
      path = "/health"
      interval = 30
      timeout = 5
      healthy_threshold = 2 
      unhealthy_threshold = 2
    }
}

# 3. Create a Target Group for the Game Server
resource "aws_lb_target_group" "game" {
    name = "memesploding-game-tg"
    port = 5204
    protocol = "HTTP"
    vpc_id = aws_vpc.main.id
    target_type = "ip" # Use IP addresses for ECS Fargate tasks
    health_check {
      path = "/health"
      interval = 30
      timeout = 5
      healthy_threshold = 2
      unhealthy_threshold = 2
    }
}

# 4. Create a Listener for the ALB
resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.main.arn
  port = 80
  protocol = "HTTP"
  default_action {
    type = "forward"
    target_group_arn = aws_lb_target_group.api.arn
  }
}

# 5. Create a Listener Rule to route traffic to the Game Server based on path
resource "aws_lb_listener_rule" "game_rule" {
  listener_arn = aws_lb_listener.http.arn # Reference the ALB listener
  action {
    type = "forward"
    target_group_arn = aws_lb_target_group.game.arn # Forward to the Game Server target group
  }

  condition {
    path_pattern {
      values = ["/ws"] # Route requests with this path to the Game Server
    }
  }
}