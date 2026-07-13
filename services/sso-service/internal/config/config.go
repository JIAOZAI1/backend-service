package config

import (
	"fmt"
	"os"
	"time"

	"github.com/spf13/viper"
)

type Config struct {
	App   AppConfig   `mapstructure:"app"`
	MySQL MySQLConfig `mapstructure:"mysql"`
	Redis RedisConfig `mapstructure:"redis"`
	JWT   JWTConfig   `mapstructure:"jwt"`
}

type AppConfig struct {
	Env  string `mapstructure:"env"`
	Port int    `mapstructure:"port"`
}

type MySQLConfig struct {
	DSN string `mapstructure:"dsn"`
}

type RedisConfig struct {
	Addr     string `mapstructure:"addr"`
	Password string `mapstructure:"password"`
	DB       int    `mapstructure:"db"`
}

type JWTConfig struct {
	Secret          string        `mapstructure:"secret"`
	Issuer          string        `mapstructure:"issuer"`
	AccessTokenTTL  time.Duration `mapstructure:"accessTokenTTL"`
	RefreshTokenTTL time.Duration `mapstructure:"refreshTokenTTL"`
}

// Load 从 configs/app.<env>.yaml 加载配置，环境变量可覆盖同名键（SSO_ 前缀）。
func Load(env string) (*Config, error) {
	v := viper.New()
	v.SetConfigName(fmt.Sprintf("app.%s", env))
	v.SetConfigType("yaml")
	v.AddConfigPath("./configs")
	v.AddConfigPath("../configs")

	v.SetEnvPrefix("SSO")
	v.AutomaticEnv()

	if err := v.ReadInConfig(); err != nil {
		return nil, fmt.Errorf("read config: %w", err)
	}

	var cfg Config
	if err := v.Unmarshal(&cfg); err != nil {
		return nil, fmt.Errorf("unmarshal config: %w", err)
	}
	cfg.App.Env = env

	// yaml 模板中的 ${VAR} 占位符由运行时环境变量展开，敏感值不落盘到配置文件
	cfg.MySQL.DSN = os.ExpandEnv(cfg.MySQL.DSN)
	cfg.Redis.Addr = os.ExpandEnv(cfg.Redis.Addr)
	cfg.Redis.Password = os.ExpandEnv(cfg.Redis.Password)
	cfg.JWT.Secret = os.ExpandEnv(cfg.JWT.Secret)

	return &cfg, nil
}
