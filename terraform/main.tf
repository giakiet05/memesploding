provider "aws" {
  region = "ap-southeast-1"

  default_tags {
    tags = {
      Project     = "memesploding"
      Environment = "dev"
      ManagedBy   = "terraform"
    }
  }
}
