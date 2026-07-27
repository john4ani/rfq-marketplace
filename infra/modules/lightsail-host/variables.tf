variable "name" {
  type = string
}

variable "availability_zone" {
  type = string
}

variable "blueprint_id" {
  type    = string
  default = "ubuntu_24_04"
}

variable "bundle_id" {
  type    = string
  default = "small_3_0"
}

variable "admin_cidr" {
  description = "IPv4 CIDR allowed to use SSH outside deployments."
  type        = string

  validation {
    condition     = can(cidrhost(var.admin_cidr, 0))
    error_message = "admin_cidr must be a valid IPv4 CIDR."
  }
}

variable "tags" {
  type    = map(string)
  default = {}
}
