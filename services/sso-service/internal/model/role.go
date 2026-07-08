package model

import "time"

const DefaultRoleName = "default"

type Role struct {
	ID          uint64     `gorm:"primaryKey" json:"id"`
	Name        string     `gorm:"column:name;uniqueIndex;size:64;not null" json:"name"`
	Description string     `gorm:"column:description;size:255;not null;default:''" json:"description"`
	CreatedAt   time.Time  `gorm:"column:created_at" json:"createdAt"`
	UpdatedAt   time.Time  `gorm:"column:updated_at" json:"updatedAt"`
	DeletedAt   *time.Time `gorm:"column:deleted_at;index" json:"-"`
}

func (Role) TableName() string {
	return "roles"
}

type UserRole struct {
	ID        uint64    `gorm:"primaryKey" json:"id"`
	UserID    uint64    `gorm:"column:user_id;not null" json:"userId"`
	RoleID    uint64    `gorm:"column:role_id;not null" json:"roleId"`
	CreatedAt time.Time `gorm:"column:created_at" json:"createdAt"`
}

func (UserRole) TableName() string {
	return "user_roles"
}
