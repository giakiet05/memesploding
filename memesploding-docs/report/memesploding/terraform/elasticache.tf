# 1. Redis Subnet Group
resource "aws_elasticache_subnet_group" "redis_subnet_group" {
  name       = "memesploding-redis-subnet-group"
  subnet_ids = [
    aws_subnet.private_subnet_1.id,
    aws_subnet.private_subnet_2.id
  ]

    tags = {
        Name = "Memesploding-Redis-Subnet-Group"
    }
}
# 2. Redis Security Group
resource "aws_security_group" "redis_sg" {
  name        = "memesploding-redis-sg"
  description = "Security group for the Redis cluster, allowing traffic from the ECS Fargate tasks"
  vpc_id      = aws_vpc.main.id

  ingress {
    description = "Allow traffic from ECS Fargate tasks to Redis"
    from_port   = 6379
    to_port     = 6379
    protocol    = "tcp"
    security_groups = [aws_security_group.ecs_sg.id] # Allow traffic from the ECS security group
  }

  egress {
    description = "Allow all outbound traffic"
    from_port   = 0
    to_port     = 0
    protocol    = "-1" # -1 means all protocols
    cidr_blocks = ["0.0.0.0/0"] 
  }
}

# 3. Create a Redis Cluster
resource "aws_elasticache_cluster" "redis" {
  cluster_id           = "memesploding-redis"
  engine               = "redis"
  node_type            = "cache.t3.micro"
  num_cache_nodes      = 1
  engine_version       = "7.1"
  port                 = 6379
  subnet_group_name    = aws_elasticache_subnet_group.redis_subnet_group.name
  security_group_ids   = [aws_security_group.redis_sg.id] # Attach the Redis

} 