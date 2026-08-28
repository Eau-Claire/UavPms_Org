from __future__ import annotations

import argparse
import base64
import hashlib
import os

import redis


def main() -> None:
    parser = argparse.ArgumentParser(description="Set a known OTP in Redis for local/test login verification.")
    parser.add_argument("--email", required=True)
    parser.add_argument("--otp", default="000000")
    parser.add_argument("--purpose", default="Login")
    parser.add_argument("--ttl", type=int, default=180)
    parser.add_argument("--redis", default=os.getenv("REDIS_CONNECTION") or os.getenv("Redis__ConnectionString") or "localhost:6379")
    parser.add_argument("--password", default=os.getenv("REDIS_PASSWORD") or None)
    args = parser.parse_args()

    host, port = parse_redis_endpoint(args.redis)
    client = redis.Redis(host=host, port=port, password=args.password, decode_responses=True)
    key = f"otp:{args.purpose.lower()}:{args.email.strip().lower()}"
    attempts_key = f"{key}:attempts"
    client.setex(key, args.ttl, sha256_base64(args.otp))
    client.delete(attempts_key)
    print(f"set {key} to known OTP {args.otp} for {args.ttl}s")


def parse_redis_endpoint(value: str) -> tuple[str, int]:
    first = value.split(",", 1)[0]
    if ":" not in first:
        return first, 6379
    host, port = first.rsplit(":", 1)
    return host, int(port)


def sha256_base64(value: str) -> str:
    return base64.b64encode(hashlib.sha256(value.encode("utf-8")).digest()).decode("ascii")


if __name__ == "__main__":
    main()
