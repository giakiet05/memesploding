# 1. Create a DB Subnet Group

resource "aws_db_subnet_group" "db_subnet_group" {
  name = "memesploding-db-subnet-group"
    subnet_ids = [
        aws_subnet.private_subnet_1.id,
        aws_subnet.private_subnet_2.id
    ]

    tags = {
        Name = "Memesploding-DB-Subnet-Group"
    }
}

# 2. Create a PostgreSQL RDS instance
resource "aws_db_instance" "postgres" {
  identifier = "memesploding-postgres"
  engine = "postgres"
  engine_version = "16"
  instance_class = "db.t3.micro"
  allocated_storage = 20

    # Network and security settings
  db_subnet_group_name = aws_db_subnet_group.db_subnet_group.name
    vpc_security_group_ids = [aws_security_group.rds_sg.id] 

    # Database credentials
    db_name = "memesplodingdb"
    username = "memesplodinguser"
    password = var.db_password # Use the variable for the password

    # Additional settings
    skip_final_snapshot = true # For development purposes; in production, you might want to take a final snapshot before deletion
    publicly_accessible = false # The database should not be publicly accessible for security reasons

    tags = {
        Name = "Memesploding-Postgres-RDS"
    }
}