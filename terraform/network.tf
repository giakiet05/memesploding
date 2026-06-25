# 1. Create a VPC
resource "aws_vpc" "main" {
    cidr_block = "10.0.0.0/16"
    tags = {
        Name = "Memesploding-VPC"
    }
}


# 2. Internet Gateway
resource "aws_internet_gateway" "igw" {
  vpc_id = aws_vpc.main.id
  tags = {
    Name = "Memesploding-IGW"
  }
}


# 3. Public Subnets
resource "aws_subnet" "public_subnet_1" {
  vpc_id            = aws_vpc.main.id
  cidr_block        = "10.0.1.0/24" # First public subnet
  availability_zone = "ap-southeast-1a" # Zone A
  map_public_ip_on_launch = true # Automatically assign public IPs to instances in this subnet
    tags = {
        Name = "Memesploding-Public-Subnet-1"
    }

}

resource "aws_subnet" "public_subnet_2" {
  vpc_id = aws_vpc.main.id
    cidr_block = "10.0.2.0/24" # Second public subnet
    availability_zone = "ap-southeast-1b" # Zone B
    map_public_ip_on_launch = true # Automatically assign public IPs to instances in this subnet
    tags = {
        Name = "Memesploding-Public-Subnet-2"
    }
}

# 4. Route Table
resource "aws_route_table" "public_rt" {
  vpc_id = aws_vpc.main.id
  route {
    cidr_block = "0.0.0.0/0" # Route all traffic to the Internet Gateway
    gateway_id = aws_internet_gateway.igw.id # Reference the Internet Gateway
  }
  tags = {
    Name = "Memesploding-Public-Route-Table"
  }
}

# 5. Associate Route Table with Public Subnets
resource "aws_route_table_association" "public_subnet_1_assoc" {
  subnet_id      = aws_subnet.public_subnet_1.id
  route_table_id = aws_route_table.public_rt.id

}

resource "aws_route_table_association" "public_subnet_2_assoc" {
  subnet_id      = aws_subnet.public_subnet_2.id
  route_table_id = aws_route_table.public_rt.id

}

# 6. Private Subnets (No public IP, no route to IGW)
resource "aws_subnet" "private_subnet_1" {
  vpc_id                  = aws_vpc.main.id
  cidr_block              = "10.0.3.0/24"
  availability_zone       = "ap-southeast-1a"
  map_public_ip_on_launch = false

  tags = { Name = "Memesploding-Private-Subnet-1" }
}

resource "aws_subnet" "private_subnet_2" {
  vpc_id                  = aws_vpc.main.id
  cidr_block              = "10.0.4.0/24"
  availability_zone       = "ap-southeast-1b"
  map_public_ip_on_launch = false

  tags = { Name = "Memesploding-Private-Subnet-2" }
}