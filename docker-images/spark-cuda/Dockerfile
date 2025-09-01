FROM nvidia/cuda:12.9.1-cudnn-devel-ubuntu24.04

# Install Java + Spark
RUN apt update -y && \
    apt install openjdk-17-jdk scala git -y
RUN apt install wget -y
RUN wget http://dlcdn.apache.org/spark/spark-4.0.0/spark-4.0.0-bin-hadoop3.tgz

ENV SPARK_HOME=/opt/spark
ENV PATH=$SPARK_HOME/bin:$PATH