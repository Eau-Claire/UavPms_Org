FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PYTHONPATH=/app

WORKDIR /app

COPY api-test/requirements.txt /app/requirements.txt
RUN pip install --no-cache-dir -r /app/requirements.txt

COPY api-test /app

RUN mkdir -p /app/reports /app/data/inspection

EXPOSE 8081

CMD ["python", "-m", "app.main"]
