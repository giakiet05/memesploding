# 1. Firewall for Load Balancer
resource "aws_security_group" "alb_sg" {
    name = "Memesploding-ALB-SG"
    description = "Security group for the Application Load Balancer, for users to access the web application"
    vpc_id = aws_vpc.main.id

    # Ingress
    ingress {
        description = "Allow HTTP traffic from anywhere on the internet"
        from_port = 80
        to_port = 80
        protocol = "tcp"
        cidr_blocks = ["0.0.0.0/0"]
    }

    ingress {
        description = "Allow HTTPS traffic from anywhere on the internet"
        from_port = 443
        to_port = 443
        protocol = "tcp"
        cidr_blocks = ["0.0.0.0/0"]
    }

    # Egress
    egress {
        description = "Allow all outbound traffic"
        from_port = 0
        to_port = 0
        protocol = "-1" # -1 means all protocols
        cidr_blocks = ["0.0.0.0/0"]
    
    }

    tags = {
        Name = "Memesploding-ALB-SG"
    }   
}

# 2. Firewall for ECS Fargate
resource "aws_security_group" "ecs_sg" {
  name = "Memesploding-ECS-SG"
  description = "Security group for the ECS Fargate tasks, allowing traffic from the ALB"
    vpc_id = aws_vpc.main.id

    ingress {
        description = "Allow traffic only from the ALB to API Server"
        from_port = 5217
        to_port = 5217
        protocol = "tcp"
        security_groups = [aws_security_group.alb_sg.id] # Allow traffic from the ALB security group
    }

    ingress {
        description = "Allow traffic only from the ALB to Game Server"
        from_port = 5204
        to_port = 5204
        protocol = "tcp"
        security_groups = [aws_security_group.alb_sg.id] # Allow traffic from the ALB security group
    }

    egress {
        description = "Allow all outbound traffic"
        from_port = 0
        to_port = 0
        protocol = "-1" # -1 means all protocols
        cidr_blocks = ["0.0.0.0/0"]
    }

    tags = {
        Name = "Memesploding-ECS-SG"
    }
}

# 3. Firewall for RDS
resource "aws_security_group" "rds_sg" {
    name = "Memesploding-RDS-SG"
    description = "Security group for the RDS database, allowing traffic from the ECS Fargate tasks"
    vpc_id = aws_vpc.main.id

    ingress {
        description = "Allow traffic only from the ECS Fargate tasks to the RDS database"
        from_port = 5432
        to_port = 5432
        protocol = "tcp"
        security_groups = [aws_security_group.ecs_sg.id] # Allow traffic from the ECS security group
    }

    egress {
        description = "Allow all outbound traffic"
        from_port = 0
        to_port = 0
        protocol = "-1" # -1 means all protocols
        cidr_blocks = ["0.0.0.0/0"]

    }
    tags = {
        Name = "Memesploding-RDS-SG"
    }
}