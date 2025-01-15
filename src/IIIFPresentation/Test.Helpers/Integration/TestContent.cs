namespace Test.Helpers.Integration;

public static class TestContent
{
    public const string ManifestJson =
        """
        {
          "@context": "https://example.com/api/presentation/3/context.json",
          "id": "https://example.com/34",
          "type": "Manifest",
          "label": {
            "en": [
              "Quidquid latinae dictum sit, altum videtur"
            ]
          },
          "thumbnail": [
            {
              "id": "https://example.com/thumbs/2/3/582382_0_0000/full/155,200/0/default.jpg",
              "type": "Image",
              "format": "image/jpeg",
              "service": [
                {
                  "@context": "https://example.com/api/image/2/context.json",
                  "@id": "https://example.com/thumbs/v2/2/3/582382_0_0000",
                  "@type": "ImageService2",
                  "profile": "https://example.com/api/image/2/level0.json",
                  "sizes": [
                    {
                      "width": 791,
                      "height": 1024
                    },
                    {
                      "width": 309,
                      "height": 400
                    },
                    {
                      "width": 155,
                      "height": 200
                    },
                    {
                      "width": 77,
                      "height": 100
                    }
                  ]
                },
                {
                  "@context": "https://example.com/api/image/3/context.json",
                  "id": "https://example.com/thumbs/2/3/582382_0_0000",
                  "type": "ImageService3",
                  "profile": "level0",
                  "sizes": [
                    {
                      "width": 791,
                      "height": 1024
                    },
                    {
                      "width": 309,
                      "height": 400
                    },
                    {
                      "width": 155,
                      "height": 200
                    },
                    {
                      "width": 77,
                      "height": 100
                    }
                  ]
                }
              ]
            }
          ],
          "homepage": [
            {
              "id": "https://example.com/title/eggplant/cornucopia",
              "type": "Text",
              "label": {
                "en": [
                  "Bazinga barnacle figure eight"
                ]
              },
              "format": "text/html",
              "language": [
                "en"
              ]
            }
          ],
          "metadata": [
            {
              "label": {
                "en": [
                  "Date"
                ]
              },
              "value": {
                "en": [
                  "February 2400"
                ]
              }
            },
            {
              "label": {
                "en": [
                  "Digital Copy Source"
                ]
              },
              "value": {
                "en": [
                  "Elbonian Bureau of Labor Statistics"
                ]
              }
            },
            {
              "label": {
                "en": [
                  "Record Created"
                ]
              },
              "value": {
                "en": [
                  "2029-03-18 08:09:32"
                ]
              }
            },
            {
              "label": {
                "en": [
                  "Record Updated"
                ]
              },
              "value": {
                "en": [
                  "2029-05-13 12:06:33"
                ]
              }
            },
            {
              "label": {
                "en": [
                  "Collection"
                ]
              },
              "value": {
                "en": [
                  "<a href=\"https://example.com/series/1486\">Bulletin of the Elbonian Bureau of Labor Statistics</a>"
                ]
              }
            },
            {
              "label": {
                "en": [
                  "Part Of"
                ]
              },
              "value": {
                "en": [
                  "<a href=\"https://example.com/title/6009\">Elbonian Compensation Survey (Eggs and Ham)</a>"
                ]
              }
            },
            {
              "label": {
                "en": [
                  "Title"
                ]
              },
              "value": {
                "en": [
                  "Blurp quarp"
                ]
              }
            },
            {
              "label": {
                "en": [
                  "Subtitle"
                ]
              },
              "value": {
                "en": [
                  "Bulletin of the Elbonian Bureau of Labor Statistics, No. 21-37"
                ]
              }
            }
          ],
          "provider": [
            {
              "id": "https://example.com/",
              "type": "Agent",
              "label": {
                "en": [
                  "Elbonian Reserve Bank of Dr Alban",
                  "Theatre and Operat",
                  "1 Elbonian Reserve Reserve Plaza",
                  "Dr Alban",
                  "HW 2137",
                  "Elbonia"
                ]
              },
              "homepage": [
                {
                  "id": "https://example.com/",
                  "type": "Text",
                  "label": {
                    "en": [
                      "Discover Economic History"
                    ]
                  },
                  "format": "text/html"
                }
              ],
              "logo": [
                {
                  "id": "https://example.com/~/media/Images/Logos/stlfed_logo.png",
                  "type": "Image",
                  "format": "image/png"
                }
              ]
            }
          ],
          "rendering": [
            {
              "id": "https://example.com/files/docs/publications/bls/bls_3100-07_1999.pdf",
              "type": "Text",
              "label": {
                "en": [
                  "Bulletin"
                ]
              },
              "format": "application/pdf"
            },
            {
              "id": "https://example.com/files/docs/publications/bls/bls_3100-07p_1999.pdf",
              "type": "Text",
              "label": {
                "en": [
                  "Percentiles"
                ]
              },
              "format": "application/pdf"
            },
            {
              "id": "https://example.com/files/text/publications/bls/bls_3100-07_1999.txt",
              "type": "Text",
              "label": {
                "en": [
                  "Bulletin Raw Text"
                ]
              },
              "format": "text/plain"
            },
            {
              "id": "https://example.com/files/text/publications/bls/bls_3100-07p_1999.txt",
              "type": "Text",
              "label": {
                "en": [
                  "Percentiles Raw Text"
                ]
              },
              "format": "text/plain"
            }
          ],
          "seeAlso": [
            {
              "id": "https://example.com/api/item/582382",
              "type": "Dataset",
              "label": {
                "en": [
                  "Fraser API"
                ]
              },
              "format": "application/json"
            }
          ],
          "service": [
            {
              "@id": "https://example.com/search/v1/item/582382",
              "@type": "SearchService1",
              "profile": "https://example.com/api/search/1/search",
              "label": "Search within this manifest",
              "service": {
                "@id": "https://example.com/search/autocomplete/v1/item/582382",
                "@type": "AutoCompleteService1",
                "profile": "https://example.com/api/search/1/autocomplete",
                "label": "Autocomplete words in this manifest"
              }
            }
          ],
          "items": [
            {
              "id": "https://example.com/presentation/item/582382/canvases/582382_0_0000",
              "type": "Canvas",
              "label": {
                "en": [
                  "Page 1"
                ]
              },
              "width": 2550,
              "height": 3300,
              "thumbnail": [
                {
                  "id": "https://example.com/thumbs/2/3/582382_0_0000/full/155,200/0/default.jpg",
                  "type": "Image",
                  "format": "image/jpeg",
                  "service": [
                    {
                      "@context": "https://example.com/api/image/2/context.json",
                      "@id": "https://example.com/thumbs/v2/2/3/582382_0_0000",
                      "@type": "ImageService2",
                      "profile": "https://example.com/api/image/2/level0.json",
                      "sizes": [
                        {
                          "width": 791,
                          "height": 1024
                        },
                        {
                          "width": 309,
                          "height": 400
                        },
                        {
                          "width": 155,
                          "height": 200
                        },
                        {
                          "width": 77,
                          "height": 100
                        }
                      ]
                    },
                    {
                      "@context": "https://example.com/api/image/3/context.json",
                      "id": "https://example.com/thumbs/2/3/582382_0_0000",
                      "type": "ImageService3",
                      "profile": "level0",
                      "sizes": [
                        {
                          "width": 791,
                          "height": 1024
                        },
                        {
                          "width": 309,
                          "height": 400
                        },
                        {
                          "width": 155,
                          "height": 200
                        },
                        {
                          "width": 77,
                          "height": 100
                        }
                      ]
                    }
                  ]
                }
              ],
              "rendering": [
                {
                  "id": "https://example.com/svg/item/582382/582382_0_0000",
                  "type": "Image",
                  "label": {
                    "en": [
                      "SVG XML for page text"
                    ]
                  },
                  "format": "image/svg+xml"
                }
              ],
              "seeAlso": [
                {
                  "id": "https://example.com/text/alto/item/582382/582382_0_0000",
                  "type": "Dataset",
                  "profile": "https://example.com/standards/alto/v3/alto.xsd",
                  "label": {
                    "none": [
                      "METS-ALTO XML"
                    ]
                  },
                  "format": "application/xml+alto"
                }
              ],
              "items": [
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0000/painting",
                  "type": "AnnotationPage",
                  "items": [
                    {
                      "id": "https://example.com/presentation/item/582382/canvases/582382_0_0000/painting/anno",
                      "type": "Annotation",
                      "motivation": "painting",
                      "body": {
                        "id": "https://example.com/iiif-img/2/3/582382_0_0000/full/791,1024/0/default.jpg",
                        "type": "Image",
                        "width": 791,
                        "height": 1024,
                        "format": "image/jpeg",
                        "service": [
                          {
                            "@context": "https://example.com/api/image/2/context.json",
                            "@id": "https://example.com/iiif-img/v2/2/3/582382_0_0000",
                            "@type": "ImageService2",
                            "profile": "https://example.com/api/image/2/level2.json",
                            "width": 2550,
                            "height": 3300
                          },
                          {
                            "@context": "https://example.com/api/image/3/context.json",
                            "id": "https://example.com/iiif-img/2/3/582382_0_0000",
                            "type": "ImageService3",
                            "profile": "level2",
                            "width": 2550,
                            "height": 3300
                          }
                        ]
                      },
                      "target": "https://example.com/presentation/item/582382/canvases/582382_0_0000"
                    }
                  ]
                }
              ],
              "annotations": [
                {
                  "id": "https://example.com/annotations/item/582382/582382_0_0000/line",
                  "type": "AnnotationPage",
                  "label": {
                    "en": [
                      "Text of page 1"
                    ]
                  }
                }
              ]
            },
            {
              "id": "https://example.com/presentation/item/582382/canvases/582382_0_0001",
              "type": "Canvas",
              "label": {
                "en": [
                  "Page 2"
                ]
              },
              "width": 2550,
              "height": 3300,
              "thumbnail": [
                {
                  "id": "https://example.com/thumbs/2/3/582382_0_0001/full/155,200/0/default.jpg",
                  "type": "Image",
                  "format": "image/jpeg",
                  "service": [
                    {
                      "@context": "https://example.com/api/image/2/context.json",
                      "@id": "https://example.com/thumbs/v2/2/3/582382_0_0001",
                      "@type": "ImageService2",
                      "profile": "https://example.com/api/image/2/level0.json",
                      "sizes": [
                        {
                          "width": 791,
                          "height": 1024
                        },
                        {
                          "width": 309,
                          "height": 400
                        },
                        {
                          "width": 155,
                          "height": 200
                        },
                        {
                          "width": 77,
                          "height": 100
                        }
                      ]
                    },
                    {
                      "@context": "https://example.com/api/image/3/context.json",
                      "id": "https://example.com/thumbs/2/3/582382_0_0001",
                      "type": "ImageService3",
                      "profile": "level0",
                      "sizes": [
                        {
                          "width": 791,
                          "height": 1024
                        },
                        {
                          "width": 309,
                          "height": 400
                        },
                        {
                          "width": 155,
                          "height": 200
                        },
                        {
                          "width": 77,
                          "height": 100
                        }
                      ]
                    }
                  ]
                }
              ],
              "rendering": [
                {
                  "id": "https://example.com/svg/item/582382/582382_0_0001",
                  "type": "Image",
                  "label": {
                    "en": [
                      "SVG XML for page text"
                    ]
                  },
                  "format": "image/svg+xml"
                }
              ],
              "seeAlso": [
                {
                  "id": "https://example.com/text/alto/item/582382/582382_0_0001",
                  "type": "Dataset",
                  "profile": "https://example.com/standards/alto/v3/alto.xsd",
                  "label": {
                    "none": [
                      "METS-ALTO XML"
                    ]
                  },
                  "format": "application/xml+alto"
                }
              ],
              "items": [
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0001/painting",
                  "type": "AnnotationPage",
                  "items": [
                    {
                      "id": "https://example.com/presentation/item/582382/canvases/582382_0_0001/painting/anno",
                      "type": "Annotation",
                      "motivation": "painting",
                      "body": {
                        "id": "https://example.com/iiif-img/2/3/582382_0_0001/full/791,1024/0/default.jpg",
                        "type": "Image",
                        "width": 791,
                        "height": 1024,
                        "format": "image/jpeg",
                        "service": [
                          {
                            "@context": "https://example.com/api/image/2/context.json",
                            "@id": "https://example.com/iiif-img/v2/2/3/582382_0_0001",
                            "@type": "ImageService2",
                            "profile": "https://example.com/api/image/2/level2.json",
                            "width": 2550,
                            "height": 3300
                          },
                          {
                            "@context": "https://example.com/api/image/3/context.json",
                            "id": "https://example.com/iiif-img/2/3/582382_0_0001",
                            "type": "ImageService3",
                            "profile": "level2",
                            "width": 2550,
                            "height": 3300
                          }
                        ]
                      },
                      "target": "https://example.com/presentation/item/582382/canvases/582382_0_0001"
                    }
                  ]
                }
              ],
              "annotations": [
                {
                  "id": "https://example.com/annotations/item/582382/582382_0_0001/line",
                  "type": "AnnotationPage",
                  "label": {
                    "en": [
                      "Text of page 2"
                    ]
                  }
                }
              ]
            },
            {
              "id": "https://example.com/presentation/item/582382/canvases/582382_0_0002",
              "type": "Canvas",
              "label": {
                "en": [
                  "Page 3"
                ]
              },
              "width": 2550,
              "height": 3300,
              "thumbnail": [
                {
                  "id": "https://example.com/thumbs/2/3/582382_0_0002/full/155,200/0/default.jpg",
                  "type": "Image",
                  "format": "image/jpeg",
                  "service": [
                    {
                      "@context": "https://example.com/api/image/2/context.json",
                      "@id": "https://example.com/thumbs/v2/2/3/582382_0_0002",
                      "@type": "ImageService2",
                      "profile": "https://example.com/api/image/2/level0.json",
                      "sizes": [
                        {
                          "width": 791,
                          "height": 1024
                        },
                        {
                          "width": 309,
                          "height": 400
                        },
                        {
                          "width": 155,
                          "height": 200
                        },
                        {
                          "width": 77,
                          "height": 100
                        }
                      ]
                    },
                    {
                      "@context": "https://example.com/api/image/3/context.json",
                      "id": "https://example.com/thumbs/2/3/582382_0_0002",
                      "type": "ImageService3",
                      "profile": "level0",
                      "sizes": [
                        {
                          "width": 791,
                          "height": 1024
                        },
                        {
                          "width": 309,
                          "height": 400
                        },
                        {
                          "width": 155,
                          "height": 200
                        },
                        {
                          "width": 77,
                          "height": 100
                        }
                      ]
                    }
                  ]
                }
              ],
              "rendering": [
                {
                  "id": "https://example.com/svg/item/582382/582382_0_0002",
                  "type": "Image",
                  "label": {
                    "en": [
                      "SVG XML for page text"
                    ]
                  },
                  "format": "image/svg+xml"
                }
              ],
              "seeAlso": [
                {
                  "id": "https://example.com/text/alto/item/582382/582382_0_0002",
                  "type": "Dataset",
                  "profile": "https://example.com/standards/alto/v3/alto.xsd",
                  "label": {
                    "none": [
                      "METS-ALTO XML"
                    ]
                  },
                  "format": "application/xml+alto"
                }
              ],
              "items": [
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0002/painting",
                  "type": "AnnotationPage",
                  "items": [
                    {
                      "id": "https://example.com/presentation/item/582382/canvases/582382_0_0002/painting/anno",
                      "type": "Annotation",
                      "motivation": "painting",
                      "body": {
                        "id": "https://example.com/iiif-img/2/3/582382_0_0002/full/791,1024/0/default.jpg",
                        "type": "Image",
                        "width": 791,
                        "height": 1024,
                        "format": "image/jpeg",
                        "service": [
                          {
                            "@context": "https://example.com/api/image/2/context.json",
                            "@id": "https://example.com/iiif-img/v2/2/3/582382_0_0002",
                            "@type": "ImageService2",
                            "profile": "https://example.com/api/image/2/level2.json",
                            "width": 2550,
                            "height": 3300
                          },
                          {
                            "@context": "https://example.com/api/image/3/context.json",
                            "id": "https://example.com/iiif-img/2/3/582382_0_0002",
                            "type": "ImageService3",
                            "profile": "level2",
                            "width": 2550,
                            "height": 3300
                          }
                        ]
                      },
                      "target": "https://example.com/presentation/item/582382/canvases/582382_0_0002"
                    }
                  ]
                }
              ],
              "annotations": [
                {
                  "id": "https://example.com/annotations/item/582382/582382_0_0002/line",
                  "type": "AnnotationPage",
                  "label": {
                    "en": [
                      "Text of page 3"
                    ]
                  }
                }
              ]
            }
          ],
          "structures": [
            {
              "id": "https://example.com/presentation/item/582382/ranges/pdf/0",
              "type": "Range",
              "label": {
                "en": [
                  "Bulletin"
                ]
              },
              "items": [
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0000",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0001",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0002",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0003",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0004",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0005",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0006",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0007",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0008",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0009",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0010",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0011",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0012",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0013",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0014",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0015",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0016",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0017",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0018",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0019",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0020",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0021",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0022",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0023",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0024",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0025",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0026",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0027",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0028",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_0_0029",
                  "type": "Canvas"
                }
              ]
            },
            {
              "id": "https://example.com/presentation/item/582382/ranges/pdf/1",
              "type": "Range",
              "label": {
                "en": [
                  "Percentiles"
                ]
              },
              "items": [
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_1_0000",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_1_0001",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_1_0002",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_1_0003",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_1_0004",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_1_0005",
                  "type": "Canvas"
                },
                {
                  "id": "https://example.com/presentation/item/582382/canvases/582382_1_0006",
                  "type": "Canvas"
                }
              ]
            }
          ],
          "annotations": [
            {
              "id": "https://example.com/annotations/item/582382/images",
              "type": "AnnotationPage",
              "label": {
                "en": [
                  "OCR-identified images and figures for 582382:item"
                ]
              }
            }
          ]
        }
        """;

    public static class Bug_158
    {
      public const string CollectionName = "my-collection";

      public const string Collection =
        """
        {
          "type": "Collection",
          "slug": "my-collection",
          "label": {
            "en": [
             "my-collection"
            ]
          },
          "thumbnail": [
            {
              "id": "https://example.com/img/thumb.jpg",
              "type": "Image",
              "format": "image/jpeg",
              "width": 512,
              "height": 256
            }
          ],
          "items": [
            {
              "id": "https://example.com/some-iiif-repo/basic_iiif_collection/collection_a",
              "type": "Collection"
            },
            {
              "id": "https://example.com/some-iiif-repo/basic_iiif_collection/collection_b",
              "type": "Collection"
            }
          ],
          "behavior": [
            "public-iiif"
          ],
          "parent": "root"
        }
        """;
    }
}
