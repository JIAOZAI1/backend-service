package model

import "time"

type User struct {
	ID           uint64     `gorm:"primaryKey" json:"id"`
	Username     string     `gorm:"column:username;uniqueIndex;size:64;not null" json:"username"`
	Email        string     `gorm:"column:email;uniqueIndex;size:128;not null" json:"email"`
	PasswordHash string     `gorm:"column:password_hash;size:255;not null" json:"-"`
	Status       int8       `gorm:"column:status;not null;default:1" json:"status"`
	ReviewStatus string     `gorm:"column:review_status;size:16;not null;default:pending" json:"reviewStatus"`
	ReviewedBy   *uint64    `gorm:"column:reviewed_by" json:"reviewedBy"`
	CreatedAt    time.Time  `gorm:"column:created_at" json:"createdAt"`
	UpdatedAt    time.Time  `gorm:"column:updated_at" json:"updatedAt"`
	DeletedAt    *time.Time `gorm:"column:deleted_at;index" json:"-"`
}

func (User) TableName() string {
	return "users"
}

const (
	UserStatusDisabled int8 = 0
	UserStatusActive   int8 = 1
)

const (
	UserReviewStatusPending  = "pending"
	UserReviewStatusApproved = "approved"
)
